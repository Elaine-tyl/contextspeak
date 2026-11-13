import { expect, test, type Page } from "@playwright/test";
import {
  gameModes,
  type GameKind,
  type GameLevelRun,
} from "@contextspeak/contracts";
import { vocabularyChallenges } from "../../apps/api/src/games/vocabulary-content";
import { textPuzzleChallenges } from "../../apps/api/src/games/text-puzzles";
import { readingOrderChallenges } from "../../apps/api/src/games/reading-order-content";
import { readingChallenges } from "../../apps/api/src/games/reading-content";

async function fixture(page: Page, kind: GameKind) {
  const activeChallenges = [
    ...vocabularyChallenges,
    ...readingChallenges,
    ...readingOrderChallenges,
    ...textPuzzleChallenges,
  ];
  const publicRound = ({
    answer,
    explanation,
    gapAnswers,
    ...round
  }: (typeof activeChallenges)[number]) => round;
  const secrets = activeChallenges
    .filter((round) => round.kind === kind && round.level === 1)
    .slice(0, 3);
  const run: GameLevelRun = {
    id: "44444444-4444-4444-8444-444444444444",
    kind,
    level: 1,
    topic: null,
    source: null,
    rounds: secrets.map(publicRound),
    receipts: [],
    completed: false,
    score: null,
    passed: false,
    nextLevel: null,
  };
  let speechRequests = 0;
  let submissions = 0;
  await page.route("**/auth/session", (route) =>
    route.fulfill({
      json: {
        csrfToken: "literacy-test",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
    }),
  );
  await page.route("**/v1/games", (route) =>
    route.fulfill({
      json: {
        rounds: activeChallenges.map(publicRound),
        progress: {
          listening: { level: 1, passed: [] },
          speaking: { level: 1, passed: [] },
        },
      },
    }),
  );
  await page.route("**/v1/speech", (route) => {
    speechRequests++;
    return route.fulfill({ status: 503, json: {} });
  });
  await page.route("**/v1/games/levels", async (route) => {
    expect(route.request().postDataJSON()).toMatchObject({ kind, level: 1 });
    await route.fulfill({ json: run });
  });
  await page.route("**/v1/games/levels/*", (route) =>
    route.fulfill({ json: run }),
  );
  await page.route("**/v1/games/attempts", async (route) => {
    const body = route.request().postDataJSON();
    expect(body.completedPlays).toBe(0);
    expect(body).not.toHaveProperty("audio");
    const secret = secrets[run.receipts.length];
    const answer = [
      "reading-order",
      "word-scramble",
      "sentence-puzzle",
    ].includes(kind)
      ? body.response
      : kind === "mini-crossword"
        ? body.gapAnswers
        : body.choice;
    expect(answer).toEqual(
      kind === "mini-crossword" ? secret.gapAnswers : secret.answer,
    );
    submissions++;
    const receipt = {
      id: `literacy-${submissions}`,
      roundId: secret.id,
      score: 100,
      createdAt: new Date().toISOString(),
      feedback: {
        explanation: secret.explanation,
        ...(kind === "mini-crossword"
          ? {
              gapFill: {
                score: 100,
                words: secret.gapAnswers!.map((word) => ({
                  answer: word,
                  expected: word,
                  correct: true,
                })),
              },
            }
          : {}),
        correctAnswer: [
          "reading-order",
          "word-scramble",
          "sentence-puzzle",
          "mini-crossword",
        ].includes(kind)
          ? secret.answer
          : secret.choices!.find((choice) => choice.id === secret.answer)!
              .label,
      },
    };
    run.receipts.push(receipt);
    if (run.receipts.length === run.rounds.length)
      Object.assign(run, {
        completed: true,
        score: 100,
        passed: true,
        nextLevel: 2,
      });
    await route.fulfill({ json: receipt });
  });
  return { run, secrets, counts: () => ({ speechRequests, submissions }) };
}

for (const kind of [
  "word-meaning",
  "word-context",
  "reading-question",
] as const) {
  test(`${kind} can start, answer and resume without playing audio`, async ({
    page,
  }, testInfo) => {
    const { run, secrets, counts } = await fixture(page, kind);
    await page.goto("http://127.0.0.1:3000/games");
    await page
      .getByRole("button", {
        name:
          kind === "reading-question" ? "Reading games" : "Vocabulary games",
        exact: true,
      })
      .click();
    await page
      .getByRole("button", {
        name: gameModes.find((mode) => mode.kind === kind)!.label,
        exact: true,
      })
      .click();
    await page
      .getByRole("button", { name: "Start level", exact: true })
      .click();
    const studio = page.getByTestId("game-studio");
    await expect(studio).toHaveAttribute("data-kind", kind);
    await expect(
      page.getByRole("button", { name: "Play challenge", exact: true }),
    ).toHaveCount(0);
    const passage = page.getByRole("region", {
      name:
        kind === "reading-question" ? "Reading passage" : "Vocabulary prompt",
      exact: true,
    });
    await expect(passage).toContainText(secrets[0].passage!.split("\n\n")[0]);
    await expect(
      page.getByRole("button", { name: "Check answer", exact: true }),
    ).toBeDisabled();
    await expect(studio.locator(".studio-visual")).toHaveAttribute(
      "data-renderer",
      "webgl",
    );
    await studio.screenshot({
      path: testInfo.outputPath(`contextspeak-${kind}.png`),
    });
    for (let index = 0; index < secrets.length; index++) {
      const secret = secrets[index];
      const correct = secret.choices!.find(
        (choice) => choice.id === secret.answer,
      )!;
      await page
        .getByRole("button", { name: correct.label, exact: true })
        .click();
      await page
        .getByRole("button", { name: "Check answer", exact: true })
        .click();
      if (index < secrets.length - 1) {
        await expect(
          page.getByRole("region", { name: "Game result" }),
        ).toContainText(secret.explanation);
      } else {
        await expect(
          page.getByRole("region", { name: "Level result" }),
        ).toContainText("100/100");
      }
      if (index === 0) {
        // Resume the same immutable run: its next unanswered task is retained.
        await page.reload();
        await expect(studio).toHaveAttribute("data-kind", kind);
      } else if (index < secrets.length - 1) {
        await page
          .getByRole("button", { name: "Next question", exact: true })
          .click();
      }
    }
    await expect(
      page.getByRole("region", { name: "Level result" }),
    ).toContainText("100/100");
    await expect(
      page.getByRole("button", { name: "Retry", exact: true }),
    ).toBeEnabled();
    await expect(
      page.getByRole("button", { name: "Next", exact: true }),
    ).toBeEnabled();
    expect(counts()).toEqual({
      speechRequests: 0,
      submissions: run.rounds.length,
    });
  });
}

test("reading order supports undo, requires all paragraphs and fits a phone", async ({
  page,
}, testInfo) => {
  const { secrets, run, counts } = await fixture(page, "reading-order");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  await expect(
    page.getByRole("region", { name: "Reading passage", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Play challenge", exact: true }),
  ).toHaveCount(0);
  const tiles = page.getByRole("group", {
    name: "Order the paragraphs",
    exact: true,
  });
  const first = secrets[0].tiles![0];
  await tiles
    .getByRole("button", { name: `Add ${first}`, exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeDisabled();
  await tiles
    .getByRole("button", { name: `Remove ${first}`, exact: true })
    .click();
  await expect(
    tiles.getByRole("button", { name: `Add ${first}`, exact: true }),
  ).toBeEnabled();
  // Recover the authored sequence from the test-only secret; it is absent from the public payload.
  const ordered = [...secrets[0].tiles!].sort(
    (a, b) => secrets[0].answer.indexOf(a) - secrets[0].answer.indexOf(b),
  );
  for (const paragraph of ordered)
    await tiles
      .getByRole("button", { name: `Add ${paragraph}`, exact: true })
      .click();
  await expect(
    page.getByRole("button", { name: "Check answer", exact: true }),
  ).toBeEnabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-reading-order-mobile.png"),
    fullPage: true,
  });
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    "100",
  );
  expect(counts()).toEqual({ speechRequests: 0, submissions: 1 });
});

for (const kind of ["word-scramble", "sentence-puzzle"] as const) {
  test(`${kind} builds a complete puzzle answer from tiles without speech`, async ({
    page,
  }, testInfo) => {
    const { secrets, run, counts } = await fixture(page, kind);
    await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
    await expect(page.getByTestId("game-studio")).toHaveAttribute(
      "data-kind",
      kind,
    );
    await expect(
      page.getByRole("button", { name: "Play challenge", exact: true }),
    ).toHaveCount(0);
    const parts =
      kind === "word-scramble"
        ? [...secrets[0].answer]
        : secrets[0].answer.split(" ");
    for (const part of parts.slice(0, -1))
      await page
        .getByRole("button", { name: `Add ${part}`, exact: true })
        .first()
        .click();
    await expect(
      page.getByRole("button", { name: "Check answer", exact: true }),
    ).toBeDisabled();
    await page
      .getByRole("button", { name: `Add ${parts.at(-1)}`, exact: true })
      .first()
      .click();
    await page
      .getByTestId("game-studio")
      .screenshot({ path: testInfo.outputPath(`contextspeak-${kind}.png`) });
    await page
      .getByRole("button", { name: "Check answer", exact: true })
      .click();
    await expect(
      page.getByRole("region", { name: "Game result" }),
    ).toContainText("100");
    expect(counts()).toEqual({ speechRequests: 0, submissions: 1 });
  });
}
test("crossword fills intersecting letters, flags conflicting guesses and submits the clue words", async ({
  page,
}, testInfo) => {
  const { secrets, run, counts } = await fixture(page, "mini-crossword");
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  const entries = secrets[0].crossword!;
  const fields = entries.map((entry, index) =>
    page.getByRole("textbox", {
      name: `Clue ${index + 1}: ${entry.clue}`,
      exact: true,
    }),
  );
  await fields[0].fill("a".repeat(entries[0].length));
  await fields[1].fill("b".repeat(entries[1].length));
  await expect(page.locator(".crossword-cell.is-conflict")).toHaveCount(1);
  await expect(
    page
      .getByRole("status")
      .filter({ hasText: "Crossing letters do not match" }),
  ).toBeVisible();
  await fields[0].fill(secrets[0].gapAnswers![0]);
  await fields[1].fill(secrets[0].gapAnswers![1]);
  await expect(page.locator(".crossword-cell.is-conflict")).toHaveCount(0);
  const columns = Math.max(
    ...entries.map(
      (entry) =>
        entry.column + (entry.direction === "across" ? entry.length : 1),
    ),
  );
  const crossingColumn = entries[1].column;
  const offset = crossingColumn === 0 ? 1 : 0;
  await page
    .locator(".crossword-grid > span")
    .nth(entries[0].row * columns + offset)
    .click();
  await expect(fields[0]).toBeFocused();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  const studioBounds = (await page.getByTestId("game-studio").boundingBox())!;
  const toggleBounds = (await page
    .getByRole("button", { name: "3D effects", exact: true })
    .boundingBox())!;
  expect(toggleBounds.y).toBeGreaterThanOrEqual(
    studioBounds.y + studioBounds.height,
  );
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({
    path: testInfo.outputPath("contextspeak-crossword-mobile.png"),
    fullPage: true,
  });
  await fields[1].press("Enter");
  await expect(
    page.getByRole("region", { name: "Crossword feedback" }),
  ).toContainText("100/100");
  expect(counts()).toEqual({ speechRequests: 0, submissions: 1 });
});
test("evidence hunt shows the complete passage before its separate evidence choices", async ({
  page,
}, testInfo) => {
  const { secrets, run, counts } = await fixture(page, "evidence-hunt");
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  const passage = page.getByRole("region", {
    name: "Reading passage",
    exact: true,
  });
  await expect(
    passage.getByRole("heading", { name: "Read the passage", exact: true }),
  ).toBeVisible();
  for (const paragraph of secrets[0].passage!.split("\n\n"))
    await expect(passage).toContainText(paragraph);
  await expect(passage.getByRole("button")).toHaveCount(0);
  const choices = page.getByRole("group", {
    name: "Choose the evidence",
    exact: true,
  });
  await expect(choices).toBeVisible();
  await expect(choices).toContainText(secrets[0].instruction);
  const options = secrets[0].choices!;
  const correct = options.find((option) => option.id === secrets[0].answer)!;
  const wrong = options.find((option) => option.id !== secrets[0].answer)!;
  await choices.getByRole("button", { name: wrong.label, exact: true }).click();
  await choices
    .getByRole("button", { name: correct.label, exact: true })
    .click();
  await expect(choices.getByRole("button", { pressed: true })).toHaveCount(1);
  await expect(
    page.getByRole("button", { name: correct.label, exact: true }),
  ).toHaveCount(1);
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByTestId("game-studio").screenshot({
    path: testInfo.outputPath("contextspeak-evidence-hunt.png"),
  });
  await page.getByRole("button", { name: "Check answer", exact: true }).click();
  await expect(page.getByRole("region", { name: "Game result" })).toContainText(
    secrets[0].explanation,
  );
  expect(counts()).toEqual({ speechRequests: 0, submissions: 1 });
});

test("saved evidence rounds without a passage field still show their complete story", async ({
  page,
}) => {
  const { run, secrets } = await fixture(page, "evidence-hunt");
  delete run.rounds[0].passage;
  await page.goto(`http://127.0.0.1:3000/games?run=${run.id}`);
  const passage = page.getByRole("region", {
    name: "Reading passage",
    exact: true,
  });
  await expect(passage).toContainText(
    secrets[0].choices!.map((choice) => choice.label).join(" "),
  );
  await expect(passage.getByRole("button")).toHaveCount(0);
  await expect(
    page
      .getByRole("group", { name: "Choose the evidence", exact: true })
      .getByRole("button"),
  ).toHaveCount(secrets[0].choices!.length);
});
