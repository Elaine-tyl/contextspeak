import { test, expect } from "@playwright/test";

for (const path of ["/library", "/pair"]) {
  test(`extension_body_attributes_do_not_interrupt_hydration_on_${path.slice(1)}`, async ({
    page,
  }) => {
    const hydrationErrors: string[] = [];
    page.on("console", (message) => {
      if (
        message.type() === "error" &&
        /hydrat|server rendered html/i.test(message.text())
      )
        hydrationErrors.push(message.text());
    });
    page.on("pageerror", (error) => {
      if (/hydrat|server rendered html/i.test(error.message))
        hydrationErrors.push(error.message);
    });
    await page.addInitScript(() => {
      // Emulate Grammarly as soon as the parser creates body, before hydration.
      const inject = () => {
        if (!document.body) return false;
        document.body.setAttribute("data-new-gr-c-s-check-loaded", "14.1344.0");
        document.body.setAttribute("data-gr-ext-installed", "");
        return true;
      };
      if (!inject()) {
        const observer = new MutationObserver(() => {
          if (inject()) observer.disconnect();
        });
        observer.observe(document, { childList: true, subtree: true });
      }
    });
    const sessionResponse = page.waitForResponse(
      (response) => response.url() === "http://127.0.0.1:4318/auth/session",
    );
    await page.goto(
      "http://127.0.0.1:3000" +
        path +
        (path === "/pair" ? "?code=ABCDEF123456" : ""),
    );
    await sessionResponse;
    await expect(page.locator("body")).toHaveAttribute(
      "data-new-gr-c-s-check-loaded",
      "14.1344.0",
    );
    expect(hydrationErrors).toEqual([]);
    if (path === "/pair") {
      await expect(page.getByLabel("Pairing code")).toHaveValue("ABCDEF123456");
    } else {
      await page.getByRole("button", { name: "Vocabulary candidates" }).click();
      await expect(
        page.getByRole("heading", {
          name: "Vocabulary candidates",
          exact: true,
        }),
      ).toBeVisible();
    }
  });
}
