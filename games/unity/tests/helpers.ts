import { expect, type Page } from "@playwright/test";

/** Complete onboarding like a new learner; later reloads must use the saved profile. */
export async function openWorld(page: Page, url: string, name?: string) {
  await page.goto(url);
  const form = page.getByRole("form", { name: "Create your character" });
  await expect(form).toBeVisible({ timeout: 120000 });
  if (name) await page.getByLabel("Character name", { exact: true }).fill(name);
  await page.getByLabel("Starting practice band").selectOption("3");
  await page
    .getByRole("button", { name: "Save & enter world", exact: true })
    .click();
  await expect(form).toBeHidden({ timeout: 120000 });
}
