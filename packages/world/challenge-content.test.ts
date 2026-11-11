import { readFile } from "node:fs/promises";
import { expect, it } from "vitest";

type Exchange = {
  prompt: string;
  goal: string;
  sample: string;
  criteria: string[];
};
type Turn = Exchange & {
  npc: { id: string; name: string; role: string };
  action: string;
  follow_up: Exchange;
};
type Scene = {
  id: string;
  title: string;
  roles: {
    id: string;
    label: string;
    npc: string;
    setting: string;
    turns: Turn[];
  }[];
};

async function catalog(): Promise<Scene[]> {
  const raw = await readFile(
    new URL("../../games/cafe/data/real_life.json", import.meta.url),
    "utf8",
  ).catch(() => null);
  expect(raw, "The playable challenge catalog must exist").not.toBeNull();
  return JSON.parse(raw!);
}

it("offers all six settings, eleven roles and thirty-three two-exchange goals", async () => {
  const scenes = await catalog();
  expect(scenes.map((scene) => scene.id)).toEqual([
    "shopping",
    "farm",
    "market",
    "restaurant",
    "amusement_park",
    "cafe",
  ]);
  expect(scenes.flatMap((scene) => scene.roles)).toHaveLength(11);
  const turns = scenes.flatMap((scene) =>
    scene.roles.flatMap((role) => role.turns),
  );
  expect(turns).toHaveLength(33);
  expect(turns.flatMap((turn) => [turn, turn.follow_up])).toHaveLength(66);
  for (const scene of scenes) {
    expect(Object.keys(scene).sort()).toEqual(["id", "roles", "title"]);
    expect(scene.title.trim()).not.toBe("");
    expect(new Set(scene.roles.map((role) => role.id)).size).toBe(
      scene.roles.length,
    );
    for (const role of scene.roles) {
      expect(Object.keys(role).sort()).toEqual([
        "id",
        "label",
        "npc",
        "setting",
        "turns",
      ]);
      expect(role.turns).toHaveLength(3);
      expect(role.label.trim()).not.toBe("");
      expect(role.npc).toContain(role.turns[0].npc.name);
    }
  }
});

it("preserves each existing role's walking order, speaker identity and reward action", async () => {
  const scenes = await catalog();
  const original: Scene[] = JSON.parse(
    await readFile(
      new URL("../../games/cafe/data/roleplay.json", import.meta.url),
      "utf8",
    ),
  );
  const routes = JSON.parse(
    await readFile(
      new URL("../../games/cafe/data/scene_routes.json", import.meta.url),
      "utf8",
    ),
  );
  for (const scene of original) {
    const challenge = scenes.find((candidate) => candidate.id === scene.id)!;
    expect(challenge.roles.map((role) => role.id)).toEqual(
      scene.roles.map((role) => role.id),
    );
    for (const role of scene.roles) {
      const turns = challenge.roles.find(
        (entry) => entry.id === role.id,
      )!.turns;
      expect(turns.map((turn) => turn.npc.id)).toEqual(
        routes[scene.id].zones.map((zone: { id: string }) => zone.id),
      );
      expect(turns.map(({ npc, action }) => ({ npc, action }))).toEqual(
        role.turns.map(({ npc, action }) => ({ npc, action })),
      );
    }
  }
});

it("keeps the cafe customer at Maya's counter twice before meeting Alex", async () => {
  const scene = (await catalog()).find((candidate) => candidate.id === "cafe")!;
  expect(scene.roles.map((role) => role.id)).toEqual(["customer"]);
  expect(scene.roles[0].turns.map((turn) => turn.npc.id)).toEqual([
    "counter",
    "counter",
    "friend",
  ]);
  expect(scene.roles[0].turns.map((turn) => turn.npc.name)).toEqual([
    "Maya",
    "Maya",
    "Alex",
  ]);
  expect(scene.roles[0].turns.map((turn) => turn.action)).toEqual([
    "basket",
    "pay",
    "table",
  ]);
});

it("gives every exchange two bounded criteria and a distinct playable follow-up", async () => {
  const scenes = await catalog();
  const { challengeCatalogSchema } = await import("./challenges");
  expect(challengeCatalogSchema.safeParse(scenes).success).toBe(true);
  const questions = new Set<string>();
  for (const scene of scenes)
    for (const role of scene.roles)
      for (const turn of role.turns) {
        expect(Object.keys(turn).sort()).toEqual([
          "action",
          "criteria",
          "follow_up",
          "goal",
          "npc",
          "prompt",
          "sample",
        ]);
        expect(Object.keys(turn.follow_up).sort()).toEqual([
          "criteria",
          "goal",
          "prompt",
          "sample",
        ]);
        for (const exchange of [turn, turn.follow_up]) {
          const context = `${scene.id}/${role.id}: ${exchange.prompt}`;
          for (const field of ["prompt", "goal", "sample"] as const) {
            expect(exchange[field].trim().length, context).toBeGreaterThan(10);
            expect(exchange[field].length, context).toBeLessThanOrEqual(240);
          }
          expect(exchange.criteria, context).toHaveLength(2);
          expect(new Set(exchange.criteria).size, context).toBe(2);
          for (const criterion of exchange.criteria) {
            expect(criterion.trim().length, context).toBeGreaterThan(5);
            expect(criterion.length, context).toBeLessThanOrEqual(160);
          }
          expect(questions.has(exchange.prompt), context).toBe(false);
          questions.add(exchange.prompt);
        }
        expect(turn.follow_up.sample).not.toBe(turn.sample);
      }
});

it("grounds role contexts in fictional US and Canadian venues with tax-inclusive prices", async () => {
  const countries = new Set<string>();
  for (const scene of await catalog())
    for (const role of scene.roles) {
      expect(role.setting.length).toBeLessThanOrEqual(400);
      expect(role.setting).toMatch(/fictional/i);
      expect(role.setting).toMatch(/United States|Canada/);
      if (role.setting.includes("Canada")) {
        countries.add("Canada");
        expect(role.setting).toMatch(/CAD/);
      } else {
        countries.add("United States");
        expect(role.setting).toMatch(/USD/);
      }
      expect(role.setting).toMatch(/tax/i);
      expect(role.setting).toMatch(/\d/);
    }
  expect([...countries].sort()).toEqual(["Canada", "United States"]);
});

it("uses natural dollar amounts in spoken questions and examples", async () => {
  for (const scene of await catalog())
    for (const role of scene.roles)
      for (const turn of role.turns)
        for (const exchange of [turn, turn.follow_up]) {
          expect(exchange.prompt).not.toMatch(/\b(?:AUD|USD|CAD)/);
          expect(exchange.sample).not.toMatch(/\b(?:AUD|USD|CAD)/);
        }
});
