import {
  test,
  expect,
  chromium,
  request as http,
  type BrowserContext,
  type Page,
} from "@playwright/test";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
let browser: BrowserContext;
let directory: string;
let tokenA: string;
let tokenB: string;
let extensionId: string;
async function provision(subject: string) {
  const client = await http.newContext();
  const origin = "http://127.0.0.1:4318";
  const start = await client.get(origin + "/auth/login", { maxRedirects: 0 });
  expect(start.status()).toBe(302);
  const authorize = new URL(start.headers().location);
  authorize.searchParams.set("test_user", subject);
  const redirect = await client.get(authorize.href, { maxRedirects: 0 });
  await client.get(redirect.headers().location, { maxRedirects: 0 });
  const session = await (await client.get(origin + "/auth/session")).json();
  const pairing = await (
    await client.post(origin + "/auth/pairings", { data: {} })
  ).json();
  const approval = await client.post(origin + "/auth/pairings/approve", {
    headers: {
      Origin: "http://127.0.0.1:3000",
      "X-CSRF-Token": session.csrfToken,
    },
    data: { userCode: pairing.userCode },
  });
  expect(approval.ok()).toBeTruthy();
  const result = await (
    await client.post(origin + "/auth/pairings/poll", {
      data: { deviceCode: pairing.deviceCode },
    })
  ).json();
  await client.dispose();
  return result.token as string;
}
async function signIn(page: Page, token: string) {
  const signedOut = page.getByRole("button", { name: "Sign out", exact: true });
  // Wait for session restoration before choosing the next test account.
  if (await signedOut.isVisible()) await signedOut.click();
  if (
    !(await page.getByRole("link", { name: "Sign in with Google" }).isVisible())
  ) {
    await expect(signedOut).toBeVisible();
    await signedOut.click();
  }
  await page.getByRole("link", { name: "Sign in with Google" }).click();
  await page
    .getByRole("link", {
      name: token === tokenB ? "Test learner B" : "Test learner A",
      exact: true,
    })
    .click();
}
async function pair(page: Page, token: string) {
  if (page.url().startsWith("chrome-extension:")) {
    const opened = browser.waitForEvent("page");
    await page
      .getByRole("button", { name: "Pair extension", exact: true })
      .click();
    const approval = await opened;
    await approval.waitForURL(/\/pair\?code=/);
    const code = await page.getByTestId("pairing-code").textContent();
    await expect(approval.getByLabel("Pairing code")).toHaveValue(code!);
    // Ensure approval happens as the requested account, independent of existing web cookies.
    const authSession = await browser.request.get(
      "http://127.0.0.1:4318/auth/session",
    );
    if (authSession.ok()) {
      const session = await authSession.json();
      await browser.request.post("http://127.0.0.1:4318/auth/logout", {
        headers: {
          Origin: "http://127.0.0.1:3000",
          "X-CSRF-Token": session.csrfToken,
        },
      });
      await approval.reload();
    }
    await approval.getByRole("link", { name: "Sign in with Google" }).click();
    await approval
      .getByRole("link", {
        name: token === tokenB ? "Test learner B" : "Test learner A",
        exact: true,
      })
      .click();
    await expect(approval.getByLabel("Pairing code")).toHaveValue(code!);
    await approval.getByRole("button", { name: "Approve extension" }).click();
    await expect(approval.getByRole("status")).toContainText(
      "Extension approved",
    );
    await expect(page.getByTestId("pairing-code")).toHaveCount(0, {
      timeout: 15000,
    });
    await approval.close();
  } else {
    await signIn(page, token);
  }
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
  tokenA = await provision("learner-a");
  tokenB = await provision("learner-b");
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
  let speechRequests = 0;
  browser.on("request", (request) => {
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/v1/speech"
    )
      speechRequests++;
    if (
      request.method() === "POST" &&
      new URL(request.url()).pathname === "/v1/translations"
    )
      transientTranslations++;
  });
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await pair(popup, tokenA);
  await popup
    .getByLabel("Automatically collect selection-translation queries", {
      exact: true,
    })
    .check();
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
  const speaker = sample.getByRole("button", {
    name: "Listen to selected text",
    exact: true,
  });
  await expect(speaker).toBeEnabled();
  await speaker.click();
  await expect.poll(() => speechRequests).toBe(1);
  await expect(speaker).toBeEnabled();
  await expect(
    sample.locator("[data-contextspeak=panel]").locator("small.speech-status"),
  ).toHaveText("");
  const audioContexts = await popup.evaluate(() =>
    chrome.runtime.getContexts({ contextTypes: ["OFFSCREEN_DOCUMENT"] }),
  );
  expect(
    audioContexts.some((context) =>
      context.documentUrl?.endsWith("/audio.html"),
    ),
  ).toBeTruthy();
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
  await expect(sample.getByRole("status")).toContainText(
    "Translation is unavailable in fixture mode",
  );
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
  await popup
    .getByLabel("Automatically collect selection-translation queries", {
      exact: true,
    })
    .check();
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
  await popup
    .getByLabel("Automatically collect selection-translation queries", {
      exact: true,
    })
    .uncheck();
  await popup.getByRole("button", { name: "Save collection settings" }).click();
  await expect(popup.getByRole("status")).toHaveText("Updated.");
  await popup
    .getByLabel("Translate automatically after selecting text", { exact: true })
    .check();
  await popup.getByRole("button", { name: "Save language settings" }).click();
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
  // Cross the auto-selection debounce boundary after the fast manual result.
  await sample.waitForTimeout(400);
  expect(transient).toBe(1);
  browser.off("request", observe);
});

test("website_signout_and_extension_revocation_are_independent", async () => {
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await pair(popup, tokenA);
  await expect(popup.getByLabel("Developer token")).toHaveCount(0);
  const worker = browser.serviceWorkers()[0];
  const extensionToken = await worker.evaluate(async () => {
    const { connection } = await (globalThis as any).chrome.storage.local.get(
      "connection",
    );
    return connection.token;
  });
  const library = await browser.newPage();
  await library.goto("http://127.0.0.1:3000/library");
  await expect(library.getByRole("button", { name: "Sign out" })).toBeVisible();
  await library.getByRole("button", { name: "Sign out" }).click();
  expect(
    (await browser.request.get("http://127.0.0.1:4318/auth/session")).status(),
  ).toBe(401);
  expect(
    (
      await browser.request.get("http://127.0.0.1:4318/v1/identity", {
        headers: { Authorization: `Bearer ${extensionToken}` },
      })
    ).status(),
  ).toBe(200);
  await signIn(library, tokenA);
  await library
    .getByText("Account & extension settings", { exact: true })
    .click();
  await library.getByRole("button", { name: "Revoke all extensions" }).click();
  await expect(library.getByRole("status")).toContainText(
    "All paired extensions revoked",
  );
  expect(
    (
      await browser.request.get("http://127.0.0.1:4318/v1/identity", {
        headers: { Authorization: `Bearer ${extensionToken}` },
      })
    ).status(),
  ).toBe(401);
  await popup.getByRole("button", { name: "Disconnect", exact: true }).click();
  await expect(popup.getByText("Not connected", { exact: true })).toBeVisible();
});

test("pairing_poll_does_not_overwrite_unsaved_popup_settings", async () => {
  const popup = await browser.newPage();
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await expect(popup.getByText("Not connected", { exact: true })).toBeVisible();
  await popup
    .getByLabel("Automatically collect selection-translation queries", {
      exact: true,
    })
    .check();
  await popup.getByLabel("Excluded origins").fill("https://private.example");
  await popup.waitForTimeout(6000);
  await expect(
    popup.getByLabel("Automatically collect selection-translation queries", {
      exact: true,
    }),
  ).toBeChecked();
  await expect(popup.getByLabel("Excluded origins")).toHaveValue(
    "https://private.example",
  );
});

test("updated_popup_explains_a_stale_background_instead_of_showing_empty_language_controls", async () => {
  const popup = await browser.newPage();
  await popup.addInitScript(() => {
    // Emulate Chrome retaining the previous worker after popup files were rebuilt.
    (globalThis as any).chrome.runtime.sendMessage = (
      message: any,
      ...args: any[]
    ) => {
      const result =
        message.type === "status"
          ? {
              ok: true,
              connection: {
                identity: "previous-worker",
                enabled: false,
                excludedOrigins: [],
              },
              records: [],
            }
          : message.type === "translation-options"
            ? { ok: false, error: "Unknown request" }
            : { ok: true };
      const callback = args.find((value) => typeof value === "function");
      if (callback) {
        queueMicrotask(() => callback(result));
        return;
      }
      return Promise.resolve(result);
    };
  });
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await expect(
    popup.getByRole("combobox", { name: "Target language", exact: true }),
  ).toHaveText("Chinese");
  await expect(popup.getByRole("alert")).toContainText(/reload.*extension/i);
  await expect(
    popup.getByRole("button", { name: "Save language settings" }),
  ).toBeDisabled();
  await popup.close();
});

test("read_only_page_containers_allow_selection_while_editable_content_stays_excluded", async () => {
  const page = await browser.newPage();
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.locator("#readonly").evaluate((element) => {
    const range = document.createRange();
    range.selectNodeContents(element);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await page.locator("#readonly").dispatchEvent("mouseup");
  await expect(
    page.getByRole("button", { name: "Translate selection", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Translate selection", exact: true })
    .click();
  await expect(page.getByRole("status")).not.toHaveText(
    "Loading translation settings…",
  );
  // A read-only island inside an editor remains private.
  await page.locator('#privacy [contenteditable="true"]').evaluate((editor) => {
    const island = document.createElement("span");
    island.contentEditable = "false";
    island.textContent = "private editor text";
    editor.append(island);
    const range = document.createRange();
    range.selectNodeContents(island);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await page.locator("#privacy").dispatchEvent("mouseup");
  await expect(
    page.getByRole("button", { name: "Translate selection", exact: true }),
  ).toHaveCount(0);
  await page.close();
});

test("selection_panel_works_when_a_page_stops_mouseup_bubbling", async () => {
  const page = await browser.newPage();
  await page.goto("http://127.0.0.1:3000/sample.html");
  await page.locator("#available").evaluate((element) => {
    element.parentElement!.addEventListener("mouseup", (event) =>
      event.stopPropagation(),
    );
    const range = document.createRange();
    range.selectNodeContents(element);
    const selection = window.getSelection()!;
    selection.removeAllRanges();
    selection.addRange(range);
  });
  await page.locator("#available").dispatchEvent("mouseup");
  await expect(
    page.getByRole("button", { name: "Translate selection", exact: true }),
  ).toBeVisible();
  await page.close();
});

test("late_language_options_cannot_overwrite_the_newly_connected_accounts_settings", async () => {
  const popup = await browser.newPage();
  await popup.addInitScript(() => {
    let account = "A",
      calls = 0;
    let releaseA: ((value: unknown) => void) | undefined;
    const listeners: ((changes: unknown, area: string) => void)[] = [];
    const options = (target: string) => ({
      ok: true,
      protocolVersion: 4,
      provider: "libretranslate-local",
      languages: [
        { code: "en", name: "English", targets: ["en", "zh-Hans"] },
        { code: "zh-Hans", name: "Chinese", targets: ["en", "zh-Hans"] },
      ],
      preferences: {
        sourceLanguage: "auto",
        targetLanguage: target,
        autoTranslate: target === "en",
      },
    });
    (globalThis as any).chrome.storage.onChanged.addListener = (
      listener: any,
    ) => listeners.push(listener);
    (globalThis as any).chrome.runtime.sendMessage = (
      message: any,
      ...args: any[]
    ) => {
      let result: unknown = { ok: true };
      if (message.type === "status")
        result = {
          ok: true,
          protocolVersion: 4,
          connection: {
            identity: account,
            enabled: false,
            excludedOrigins: [],
          },
          records: [],
        };
      if (message.type === "translation-options")
        result =
          ++calls === 1
            ? new Promise((resolve) => {
                releaseA = resolve;
              })
            : options("zh-Hans");
      const callback = args.find((value) => typeof value === "function");
      if (callback) {
        Promise.resolve(result).then(callback);
        return;
      }
      return Promise.resolve(result);
    };
    (globalThis as any).languageOptionsCalls = () => calls;
    (globalThis as any).switchTranslationAccount = () => {
      account = "B";
      for (const listener of listeners)
        listener(
          {
            connection: { oldValue: { token: "A" }, newValue: { token: "B" } },
          },
          "local",
        );
    };
    (globalThis as any).releaseOldLanguageOptions = () =>
      releaseA?.(options("en"));
  });
  await popup.goto(`chrome-extension://${extensionId}/popup.html`);
  await expect
    .poll(() =>
      popup.evaluate(() => (globalThis as any).languageOptionsCalls()),
    )
    .toBe(1);
  await popup.evaluate(() => (globalThis as any).switchTranslationAccount());
  await expect(
    popup.getByRole("button", { name: "Save language settings" }),
  ).toBeEnabled();
  await expect(
    popup.getByLabel("Target language", { exact: true }),
  ).toHaveAttribute("data-value", "zh-Hans");
  await popup.evaluate(() => (globalThis as any).releaseOldLanguageOptions());
  // Let the deliberately late response cross React's render boundary.
  await popup.waitForTimeout(100);
  await expect(
    popup.getByLabel("Target language", { exact: true }),
  ).toHaveAttribute("data-value", "zh-Hans");
  await expect(
    popup.getByLabel("Translate automatically after selecting text", {
      exact: true,
    }),
  ).not.toBeChecked();
  await popup.close();
});
