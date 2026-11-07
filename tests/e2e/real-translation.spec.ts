import { test, expect, chromium } from "@playwright/test";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

test("free_translation_supports_sentences_automatic_selection_and_saved_languages", async () => {
  const profile = await mkdtemp(join(tmpdir(), "cs-real-translation-"));
  const extension = resolve("apps/extension/.output/chrome-mv3");
  const browser = await chromium.launchPersistentContext(profile, {
    channel: "chromium",
    headless: true,
    args: [
      `--disable-extensions-except=${extension}`,
      `--load-extension=${extension}`,
    ],
  });
  try {
    const worker =
      browser.serviceWorkers()[0] ??
      (await browser.waitForEvent("serviceworker"));
    const popup = await browser.newPage();
    await popup.goto(
      `chrome-extension://${new URL(worker.url()).host}/popup.html`,
    );
    const opened = browser.waitForEvent("page");
    await popup
      .getByRole("button", { name: "Pair extension", exact: true })
      .click();
    const approval = await opened;
    await approval.getByRole("link", { name: "Sign in with Google" }).click();
    await approval
      .getByRole("link", { name: "Test learner A", exact: true })
      .click();
    await approval.getByRole("button", { name: "Approve extension" }).click();
    await expect(popup.getByText("Connected", { exact: true })).toBeVisible();
    await popup
      .getByLabel("Source language", { exact: true })
      .selectOption("auto");
    await popup
      .getByLabel("Target language", { exact: true })
      .selectOption("zh-Hans");
    await popup
      .getByLabel("Translate automatically after selecting text", {
        exact: true,
      })
      .check();
    await popup.getByRole("button", { name: "Save language settings" }).click();
    await expect(popup.getByRole("status")).toHaveText("Updated.");
    await popup
      .getByLabel("Automatically collect selection-translation queries", {
        exact: true,
      })
      .check();
    await popup
      .getByRole("button", { name: "Save collection settings" })
      .click();
    await expect(popup.getByRole("status")).toHaveText("Updated.");
    const token = await worker.evaluate(
      async () =>
        (await (globalThis as any).chrome.storage.local.get("connection"))
          .connection.token,
    );
    const headers = { Authorization: `Bearer ${token}` };
    const sample = await browser.newPage();
    await sample.goto("http://127.0.0.1:3000/sample.html");
    async function select(id: string) {
      await sample.locator("#" + id).evaluate((element) => {
        const range = document.createRange();
        range.selectNodeContents(element);
        const selection = window.getSelection()!;
        selection.removeAllRanges();
        selection.addRange(range);
      });
      await sample.locator("#" + id).dispatchEvent("mouseup");
    }
    await select("sentence");
    // No button click: this must use the saved automatic-translation preference.
    await expect(sample.getByRole("status")).toContainText(/[\u3400-\u9fff]/);
    const first = (
      await (
        await browser.request.get("http://127.0.0.1:4318/v1/lookups", {
          headers,
        })
      ).json()
    ).items[0];
    expect(first).toMatchObject({
      sourceLanguage: "auto",
      targetLanguage: "zh-Hans",
      detectedSourceLanguage: "en",
      translationStatus: "succeeded",
    });
    const library = await browser.newPage();
    await library.goto("http://127.0.0.1:3000/library");
    await expect(library.getByTestId("lookup-card")).toContainText(
      first.meaning,
    );
    await expect(library.getByTestId("candidate-card")).toHaveCount(1);
    await expect(sample.getByRole("status")).toHaveText(first.meaning);
    // A repeated encounter shares the candidate, but keeps its own stored result.
    await select("sentence");
    await expect(sample.getByRole("status")).toContainText(/[\u3400-\u9fff]/);
    const repeated = await (
      await browser.request.get("http://127.0.0.1:4318/v1/lookups", { headers })
    ).json();
    expect(repeated.items).toHaveLength(2);
    expect(
      (
        await (
          await browser.request.get("http://127.0.0.1:4318/v1/learning-items", {
            headers,
          })
        ).json()
      ).items,
    ).toHaveLength(1);
    // Save a new target, then reopen the popup to prove persistence.
    await popup
      .getByLabel("Translate automatically after selecting text", {
        exact: true,
      })
      .uncheck();
    await popup
      .getByLabel("Target language", { exact: true })
      .selectOption("en");
    await popup.getByRole("button", { name: "Save language settings" }).click();
    await expect(popup.getByRole("status")).toHaveText("Updated.");
    await popup.reload();
    await expect(
      popup.getByLabel("Target language", { exact: true }),
    ).toHaveValue("en");
    await select("chinese");
    await sample
      .getByRole("button", { name: "Translate selection", exact: true })
      .click();
    await expect(sample.getByRole("status")).toContainText(/train station/i);
    const reversed = (
      await (
        await browser.request.get("http://127.0.0.1:4318/v1/lookups", {
          headers,
        })
      ).json()
    ).items[0];
    expect(reversed).toMatchObject({
      sourceLanguage: "auto",
      targetLanguage: "en",
      detectedSourceLanguage: "zh-Hans",
      translationStatus: "succeeded",
    });
    await library.getByRole("button", { name: "Refresh", exact: true }).click();
    await expect(library.getByTestId("lookup-card")).toHaveCount(3);
  } finally {
    await browser.close();
    await rm(profile, { recursive: true, force: true });
  }
});

test("translator_shutdown_during_startup_stops_the_spawned_model_process", async () => {
  const { createServer } = await import("node:net");
  const { spawn } = await import("node:child_process");
  const socket = createServer();
  await new Promise<void>((r) => socket.listen(0, "127.0.0.1", r));
  const address = socket.address();
  if (!address || typeof address === "string") throw new Error("No free port");
  const port = address.port;
  await new Promise<void>((r) => socket.close(() => r()));
  const launcher = spawn(
    process.execPath,
    ["--import", "tsx", "scripts/translator.ts", "--port", String(port)],
    { stdio: ["ignore", "pipe", "pipe"] },
  );
  let childPid: number | undefined;
  let output = "";
  try {
    childPid = await new Promise<number>((resolve, reject) => {
      launcher.stdout.on("data", (chunk) => {
        output += String(chunk);
        const match = output.match(/Starting local translator \(PID (\d+)\)/);
        if (match) resolve(Number(match[1]));
      });
      launcher.once("error", reject);
      launcher.once("exit", () =>
        reject(new Error("Launcher exited before spawning translator")),
      );
    });
    expect(output).not.toContain("Free local translator:");
    const exited = new Promise<number | null>((r) => launcher.once("exit", r));
    launcher.kill("SIGTERM");
    expect(await exited).toBe(0);
    await expect
      .poll(() => {
        try {
          process.kill(childPid!, 0);
          return true;
        } catch {
          return false;
        }
      })
      .toBe(false);
  } finally {
    launcher.kill("SIGTERM");
    if (childPid) {
      try {
        process.kill(childPid, "SIGTERM");
      } catch {}
    }
  }
});
