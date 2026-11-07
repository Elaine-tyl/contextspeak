import {
  test,
  expect,
  chromium,
  type BrowserContext,
  type Page,
} from "@playwright/test";
import { readFile, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
let browser: BrowserContext;
let directory: string;
let tokenA: string;
let tokenB: string;
let extensionId: string;
async function pair(page: Page, token: string) {
  if (!(await page.getByLabel("Developer token", { exact: true }).isVisible()))
    await page.locator("summary").click();
  await page.getByLabel("Developer token", { exact: true }).fill(token);
  await page.getByRole("button", { name: "Connect", exact: true }).click();
  await expect(page.getByText("Connected", { exact: true })).toBeVisible();
}
async function select(page: Page, id: string) {
  await page.locator("#" + id).evaluate((el) => {
    const range = document.createRange();
    range.selectNodeContents(el);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await page.locator("#" + id).dispatchEvent("mouseup");
  await page
    .getByRole("button", { name: "Translate selection", exact: true })
    .click();
}
test.beforeAll(async () => {
  ({ tokenA, tokenB } = JSON.parse(await readFile(".local/demo.json", "utf8")));
  directory = await mkdtemp(join(tmpdir(), "cs-browser-"));
  const path = resolve("apps/extension/.output/chrome-mv3");
  browser = await chromium.launchPersistentContext(directory, {
    channel: "chromium",
    headless: true,
    args: [`--disable-extensions-except=${path}`, `--load-extension=${path}`],
  });
  const worker =
    browser.serviceWorkers()[0] ??
    (await browser.waitForEvent("serviceworker"));
  extensionId = new URL(worker.url()).host;
});
test.afterAll(async () => {
  await browser?.close();
  if (directory) await rm(directory, { recursive: true, force: true });
});
test("capture_to_library_is_owned_replay_safe_and_failure_tolerant", async () => {
  let transientTranslations = 0;
  browser.on("request", (request) => {
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/v1/translations"
    )
      transientTranslations++;
  });
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await pair(popup, tokenA);
  await popup.getByRole("checkbox").check();
  await popup.getByRole("button", { name: "Save collection settings" }).click();
  await expect(popup.getByRole("status")).toHaveText("Updated.");
  const library = await browser.newPage();
  await library.goto("http://127.0.0.1:3000/library");
  await pair(library, tokenA);
  const sample = await browser.newPage();
  await sample.goto("http://127.0.0.1:3000/sample.html");
  await select(sample, "available");
  await expect(sample.getByRole("status")).toHaveText(
    "Free to take part or ready to be used.",
  );
  expect(transientTranslations).toBe(0);
  await library.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(library.getByTestId("lookup-card")).toHaveCount(1);
  await expect(library.getByTestId("candidate-card")).toHaveCount(1);
  const api = async (
    path: string,
    method = "GET",
    body?: unknown,
    token = tokenA,
  ) => {
    const r = await browser.request.fetch("http://127.0.0.1:4318" + path, {
      method,
      headers: { Authorization: `Bearer ${token}` },
      data: body,
    });
    expect(r.ok()).toBeTruthy();
    return r.json();
  };
  const first = (await api("/v1/lookups")).items[0];
  await api("/v1/lookups", "POST", {
    eventId: first.eventId,
    text: first.text,
    context: first.context,
    sourceUrl: first.sourceUrl,
    sourceLanguage: "en",
    targetLanguage: "en",
  });
  expect((await api("/v1/lookups")).items).toHaveLength(1);
  await select(sample, "available");
  await expect
    .poll(async () => (await api("/v1/lookups")).items.length)
    .toBe(2);
  await library.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(library.getByTestId("lookup-card")).toHaveCount(2);
  await expect(library.getByTestId("candidate-card")).toHaveCount(1);
  await select(sample, "unknown");
  await expect(sample.getByRole("status")).toContainText("Fixture unavailable");
  await expect
    .poll(
      async () =>
        (await api("/v1/lookups")).items.find(
          (x: any) => x.text === "unknown-fixture",
        )?.translationStatus,
    )
    .toBe("failed");
  await library.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(
    library.getByTestId("lookup-card").filter({ hasText: "unknown-fixture" }),
  ).toContainText("FIXTURE_NOT_FOUND");
  await library.screenshot({
    path: ".local/library-desktop.png",
    fullPage: true,
  });
  await library.setViewportSize({ width: 390, height: 844 });
  await expect(
    library.getByRole("heading", { name: "Recent encounters", exact: true }),
  ).toBeVisible();
  expect(
    await library.evaluate(
      () => document.documentElement.scrollWidth <= window.innerWidth,
    ),
  ).toBeTruthy();
  await library.screenshot({
    path: ".local/library-mobile.png",
    fullPage: true,
  });
  await library.setViewportSize({ width: 1280, height: 900 });
  await pair(library, tokenB);
  await expect(library.getByTestId("lookup-card")).toHaveCount(0);
  await expect(library.getByTestId("candidate-card")).toHaveCount(0);
  expect(
    (await api("/v1/lookups", "GET", undefined, tokenB)).items,
  ).toHaveLength(0);
  // A queued query survives a worker/browser restart. Offline translation still records intent.
  await browser.setOffline(true);
  await select(sample, "reschedule");
  await expect(sample.getByRole("status")).toContainText(/offline/i);
  await browser.close();
  browser = await chromium.launchPersistentContext(directory, {
    channel: "chromium",
    headless: true,
    args: [
      `--disable-extensions-except=${resolve("apps/extension/.output/chrome-mv3")}`,
      `--load-extension=${resolve("apps/extension/.output/chrome-mv3")}`,
    ],
  });
  const resumed = await browser.newPage();
  await resumed.goto(`chrome-extension://${extensionId}/popup.html`);
  await resumed.getByRole("button", { name: "Sync now" }).click();
  await expect
    .poll(async () => {
      const r = await browser.request.get("http://127.0.0.1:4318/v1/lookups", {
        headers: { Authorization: `Bearer ${tokenA}` },
      });
      return (await r.json()).items.filter((x: any) => x.text === "reschedule")
        .length;
    })
    .toBe(1);
});

test("editable_descendants_are_excluded_from_context_and_crossing_selections", async () => {
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await pair(popup, tokenA);
  await popup.getByRole("checkbox").check();
  await popup.getByRole("button", { name: "Save collection settings" }).click();
  await expect(popup.getByRole("status")).toHaveText("Updated.");
  const sample = await browser.newPage();
  await sample.goto("http://127.0.0.1:3000/sample.html");
  await sample.locator("#privacy").evaluate((el) => {
    const range = document.createRange();
    range.selectNodeContents(el.firstChild!);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await sample.locator("#privacy").dispatchEvent("mouseup");
  await sample
    .getByRole("button", { name: "Translate selection", exact: true })
    .click();
  await expect
    .poll(async () => {
      const response = await browser.request.get(
        "http://127.0.0.1:4318/v1/lookups?limit=100",
        { headers: { Authorization: `Bearer ${tokenA}` } },
      );
      return (
        (await response.json()).items.find((x: any) => x.text === "available")
          ?.context ?? "missing"
      );
    })
    .toBe("available");
  const response = await browser.request.get(
    "http://127.0.0.1:4318/v1/lookups?limit=100",
    { headers: { Authorization: `Bearer ${tokenA}` } },
  );
  expect(
    (await response.json()).items.some((x: any) => x.text === "available"),
  ).toBeTruthy();
  await sample.locator("#privacy").evaluate((el) => {
    const range = document.createRange();
    range.selectNodeContents(el);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await sample.locator("#privacy").dispatchEvent("mouseup");
  await expect(
    sample.getByRole("button", { name: "Translate selection", exact: true }),
  ).toHaveCount(0);
});

test("loaded_history_remains_visible_across_the_polling_interval", async () => {
  const headers = { Authorization: `Bearer ${tokenB}` };
  await browser.request.put("http://127.0.0.1:4318/v1/settings", {
    headers,
    data: { enabled: true, excludedOrigins: [] },
  });
  for (let i = 0; i < 21; i++) {
    const response = await browser.request.post(
      "http://127.0.0.1:4318/v1/lookups",
      {
        headers,
        data: {
          eventId: crypto.randomUUID(),
          text: "available",
          context: `Pagination example ${i}`,
          sourceLanguage: "en",
          targetLanguage: "en",
        },
      },
    );
    expect(response.status()).toBe(201);
  }
  const page = await browser.newPage();
  await page.goto("http://127.0.0.1:3000/library");
  await pair(page, tokenB);
  await expect(page.getByTestId("lookup-card")).toHaveCount(20);
  await page.getByRole("button", { name: "Load more", exact: true }).click();
  await expect(page.getByTestId("lookup-card")).toHaveCount(21);
  // Observe the actual three-second polling boundary, not only the immediate append.
  await page.waitForTimeout(3500);
  await expect(page.getByTestId("lookup-card")).toHaveCount(21);
});

test("collection_off_uses_one_transient_translation_without_history", async () => {
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await pair(popup, tokenA);
  await popup.getByRole("checkbox").uncheck();
  await popup.getByRole("button", { name: "Save collection settings" }).click();
  await expect(popup.getByRole("status")).toHaveText("Updated.");
  const headers = { Authorization: `Bearer ${tokenA}` };
  const before = (
    await (
      await browser.request.get("http://127.0.0.1:4318/v1/lookups?limit=100", {
        headers,
      })
    ).json()
  ).items.length;
  let transient = 0;
  const observe = (request: any) => {
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/v1/translations"
    )
      transient++;
  };
  browser.on("request", observe);
  const sample = await browser.newPage();
  await sample.goto("http://127.0.0.1:3000/sample.html");
  await select(sample, "available");
  await expect(sample.getByRole("status")).toHaveText(
    "Free to take part or ready to be used.",
  );
  const after = (
    await (
      await browser.request.get("http://127.0.0.1:4318/v1/lookups?limit=100", {
        headers,
      })
    ).json()
  ).items.length;
  expect(after).toBe(before);
  expect(transient).toBe(1);
  browser.off("request", observe);
});
