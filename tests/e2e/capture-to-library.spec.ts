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
  await expect(sample.getByRole("status")).toContainText("offline");
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
