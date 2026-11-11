import { z } from "zod";
import catalog from "../../games/cafe/data/roleplay.json";
import {
  CHALLENGE_SCENES,
  challengeSceneId,
  challengeChecksSchema,
  challengeChecksFor,
  normalizeRoleplayText as normalize,
  roleplayMeaningWords as meaningWords,
  roleplayInstructionsOrHelp as instructionsOrHelp,
  unsupportedRoleplayFeedback as unsupported,
  reviewChallenge,
} from "./challenges";

const sceneId = z.enum([
  "shopping",
  "farm",
  "market",
  "restaurant",
  "amusement_park",
]);
// IDs are dialogue routes, shared with the authored walking scene. Physical
// positions remain in scene_routes.json and never come from model output.
const sceneNpcRoutes = {
  shopping: ["clothes", "sale", "fitting"],
  farm: ["fields", "farm_shop", "packing"],
  market: ["fruit_stall", "produce_stall", "checkout"],
  restaurant: ["entrance", "dining", "till"],
  amusement_park: ["tickets", "rides", "snacks"],
} satisfies Record<z.infer<typeof sceneId>, string[]>;
const turnSchema = z.strictObject({
  npc: z.strictObject({
    id: z.string().trim().min(1).max(40),
    name: z.string().trim().min(1).max(80),
    role: z.string().trim().min(1).max(80),
  }),
  prompt: z.string().trim().min(1).max(240),
  goal: z.string().trim().min(1).max(240),
  sample: z.string().trim().min(1).max(240),
  action: z.enum([
    "outfit",
    "price",
    "bags",
    "ride",
    "tickets",
    "snack",
    "meet",
    "water",
    "harvest",
    "basket",
    "pay",
    "meal",
    "table",
  ]),
});
const roleSchema = z.strictObject({
  id: z.string().min(1).max(40),
  label: z.string().trim().min(1).max(80),
  npc: z.string().trim().min(1).max(80),
  turns: z
    .array(turnSchema)
    .length(3)
    .refine(
      (turns) =>
        new Set(turns.map((turn) => turn.npc.id)).size === turns.length,
      "Each turn must use a distinct NPC",
    ),
});

/** Catalog validation is shared by authored content and its contract tests. */
export const roleplayCatalogSchema = z
  .array(
    z
      .strictObject({
        id: sceneId,
        title: z.string().trim().min(1).max(80),
        roles: z
          .array(roleSchema)
          .length(2)
          .refine(
            (roles) =>
              new Set(roles.map((role) => role.id)).size === roles.length,
            "Role IDs must be unique within a scene",
          ),
      })
      .superRefine((scene, context) => {
        const routes = sceneNpcRoutes[scene.id];
        scene.roles.forEach((role, roleIndex) => {
          role.turns.forEach((turn, turnIndex) => {
            if (turn.npc.id !== routes[turnIndex])
              context.addIssue({
                code: "custom",
                path: ["roles", roleIndex, "turns", turnIndex, "npc", "id"],
                message: "NPC must match the current scene route and turn",
              });
          });
        });
      }),
  )
  .length(5)
  .refine(
    (scenes) => new Set(scenes.map((scene) => scene.id)).size === 5,
    "Scene IDs must be unique",
  );

/** Shared authored content; the model never supplies actions or the next turn. */
export const ROLEPLAY_SCENES = roleplayCatalogSchema.parse(catalog);

export const roleplayInput = z
  .strictObject({
    scene: challengeSceneId,
    role: z.string().min(1).max(40),
    turn: z.number().int().min(0).max(2),
    text: z.string().trim().min(1).max(400),
    history: z.array(z.string().trim().min(1).max(400)).max(5),
    difficulty: z.enum(["everyday", "challenge"]).optional(),
    exchange: z.number().int().min(0).max(1).optional(),
  })
  .refine(
    (input) =>
      input.difficulty === "challenge"
        ? input.history.length === input.turn * 2 + (input.exchange ?? 0)
        : (input.exchange ?? 0) === 0 && input.history.length === input.turn,
    "History must match the current turn",
  )
  .refine(
    (input) =>
      (input.difficulty === "challenge"
        ? CHALLENGE_SCENES
        : ROLEPLAY_SCENES
      ).some(
        (scene) =>
          scene.id === input.scene &&
          scene.roles.some((role) => role.id === input.role),
      ),
    "Choose a role from the current scene",
  );

export const roleplayModelOutput = z.strictObject({
  accepted: z.boolean(),
  correction: z.string().trim().max(240),
  feedback: z.string().trim().min(1).max(240),
  acknowledgement: z.string().trim().min(1).max(160),
});
export const challengeModelOutput = roleplayModelOutput.extend({
  checks: challengeChecksSchema,
});
export const challengeModelOutputFor = (text: string) =>
  challengeModelOutput.extend({
    checks: challengeChecksFor(text),
  });
export const roleplayOutput = roleplayModelOutput.refine(
  (output) => output.accepted || output.correction.length > 0,
  "A retry needs a corrected answer",
);

type Input = z.infer<typeof roleplayInput>;
type Output = z.infer<typeof roleplayOutput>;

export function roleplayContext(input: Input) {
  const scene = (
    input.difficulty === "challenge" ? CHALLENGE_SCENES : ROLEPLAY_SCENES
  ).find((scene) => scene.id === input.scene)!;
  const role = scene.roles.find((role) => role.id === input.role)!;
  const primary = role.turns[input.turn];
  const exchange = input.exchange ?? 0;
  const turn =
    exchange === 1 && "follow_up" in primary
      ? { ...primary, ...primary.follow_up }
      : primary;
  return { scene, role, turn, exchange };
}

const request =
  /\b(?:i(?: would)? (?:like|want|need)|i will (?:take|have)|(?:can|could|may) (?:i|we)|(?:can|could|would) you (?:give|bring|get)|please (?:give|bring)|(?:a|an|the) .+please)\b|^(?:[a-z0-9]+\s+){1,4}please$/;
const intention =
  /\b(?:i(?: will| am going to| want to| would like to| plan to)|let me)\b/;
const cash = [/\bcash\b/, /\b(?:pay|cash please|in cash|with cash|by cash)\b/];
const clothing =
  /\b(?:hoodies?|sweatshirts?|shirts?|t shirts?|tees?|jackets?|coats?|sweaters?|jumpers?|cardigans?|trousers|pants|sweatpants|joggers|jeans|shorts|skirts?|dress(?:es)?|suits?|socks|shoes|sneakers|trainers|boots|sandals|hats?|caps?|scarves|scarf|beanies?|pyjamas|pajamas|leggings|tracksuits?|vests?|tops?|blouses?|uniforms?|underwear|swimsuits?)\b/;
const ride =
  /\b(?:rides?|coasters?|wheel|carousel|merry go round|bumper cars?|dodgems?|water slides?|log flume|drop tower|teacups?|swings?|pirate ship|train|boats?|haunted house|fun house|lazy river|go karts?)\b/;
const priceOpinion =
  /\b(?:price|pricey|expensive|cheap|cheaper|cost|worth|afford|money|budget|sale|bargain|steep|reasonable|fair|deal|dollars?|bucks?|too much|bit much|fine|okay|ok|good|great|nice|yes|no way|like it|love it)\b/;
const shoppingPlan =
  /\b(?:try|shop|shops|shopping|browse|browsing|look|looking|fitting|another|else|keep|search|next|cheaper|sales?|wait|save|leave|skip|pass|discount|compare|online)\b/;
// Open choices cannot be an exhaustive vocabulary list. Concise replies and
// natural suggestions still need the model's on-topic verdict.
const naturalChoice =
  /^(?:[a-z0-9']+ ){0,3}[a-z0-9']+$|^(?:how about|what about|maybe|let's|i (?:am looking for|am after|want|need|would like|will take)|we (?:could|should|can|will|want|need))\b/;
const sampleChoices = new RegExp(
  `${clothing.source}|${ride.source}|\\b(?:blue|green|popcorn|sale rack)\\b`,
  "g",
);

// These are conservative goal checks, not a grammar scorer. They prevent an
// over-permissive small model from accepting unrelated words or missing slots.
// Friend scenarios use broad topic checks; the model judges the reply's meaning
// and accepts sensible alternatives rather than requiring sample-answer words.
const evidence: Record<string, RegExp[][]> = {
  "shopping/shopper": [[clothing], [priceOpinion], [shoppingPlan]],
  "shopping/buddy": [
    [clothing],
    [
      /\b(?:red|blue|green|yellow|orange|purple|pink|black|white|brown|grey|gray|navy|beige|color|colour|looks?|suit|suits|like|love|prefer|nice|good|great|better|bright|dark|light|fine|okay|ok|yes|no)\b/,
    ],
    [shoppingPlan],
  ],
  "amusement_park/visitor": [
    [ride],
    [
      /\b(?:no worries|no pressure|don't worry|do not worry|okay|ok|fine|safe|together|with you|easy|slow|gentle|smaller|small|skip|break|rest|show|game|walk|sit|arcade|instead|don't have to|do not have to)\b|\b(?:rides?|coasters?|wheel|carousel|bumper cars?|flume|teacups?|swings?|train|boats?)\b/,
    ],
    [
      /\b(?:food|snacks?|popcorn|ice cream|cotton candy|candy floss|churros?|burgers?|hot dogs?|sandwich|sandwiches|fries|nachos|waffles?|noodles?|rice|pizza|salad|pretzels?|donuts?|doughnuts?|tacos?|cake|chips|water|soda|lemonade|milkshakes?|smoothies?|coffee|tea|juice|drinks?|vegetarian|vegan)\b/,
    ],
  ],
  "amusement_park/planner": [
    [/\b(?:[1-9][0-9]?|couple|pair|both|each|two of us)\b/],
    [
      /\b(?:wait|waiting|queue|line|later|come back|try|another|instead|skip|fast pass|fastpass|express|reservation|eat|lunch|snack|break)\b|\b(?:rides?|coasters?|wheel|carousel|bumper cars?|flume|teacups?|swings?|train|boats?)\b/,
    ],
    [
      /\b(?:entrance|exit|gates?|ticket booth|fountain|wheel|carousel|castle|map|visitor cent(?:er|re)|information|info|desk|statue|bench|cafe|coffee shop|stand|restrooms?|toilets?|coaster|tree|here)\b/,
    ],
  ],
  "farm/helper": [
    [intention, /\bwater\b/, /\bcarrots?\b/],
    [intention, /\b(?:pick|harvest|collect)\b/, /\bripe\b/, /\btomatoes?\b/],
    [
      /\b(?:put|place|leave)\b/,
      /\b(?:tomatoes?|them)\b/,
      /\b(?:in|into) (?:the |a |my )?basket\b/,
    ],
  ],
  "farm/farm_shop_customer": [
    [request, /\b(?:six|6|half a dozen)\b/, /\beggs?\b/],
    [request, /\b(?:two|2)\b/, /\bcarrots?\b/],
    cash,
  ],
  "market/shopper": [
    [request, /\b(?:three|3)\b/, /\bapples?\b/],
    [request, /\bbag\b/],
    cash,
  ],
  "market/vendor": [
    [
      /\b(?:apples?|they|those|it)\b|^(?:three|3)\b/,
      /\b(?:three|3)\s*(?:dollars?|bucks?)\b|\$\s*3\b/,
    ],
    [/\b(?:here|these|take|your|for you)\b/, /\b(?:two|2)\b/, /\bapples?\b/],
    [/\b(?:yes|can|accept|welcome|sure|certainly)\b/, /\bcards?\b/],
  ],
  "restaurant/customer": [
    [/\btable\b/, /\b(?:two|2)\b/, /\b(?:please|could|can|like|need|want)\b/],
    [request, /\b(?:vegetable|veggie)\b/, /\bsoup\b/],
    [request, /\b(?:bill|check)\b/],
  ],
  "restaurant/server": [
    [/\b(?:follow me|come with me|this way)\b/, /\btable\b/],
    [/\b(?:recommend|suggest|try)\b/, /\b(?:vegetable|veggie)\b/, /\bsoup\b/],
    [
      /\b(?:bill|total|that|it)\b/,
      /\b(?:ten|10)\s*(?:dollars?|bucks?)\b|\$\s*10\b/,
    ],
  ],
};

const negatedGoal =
  /\b(?:do not|don't|dont|never|will not|won't|wont|can't|cannot)\s+(?:want|need|like|have|order|pay|pick|harvest|water|study|open|follow|put|take|give|recommend)\b|\bnot\s+(?:an?\s+)?(?:english|six|two|three|ten|nine|five|vegetable|cash|card)\b/;

/** Keep a useful repair line even when the model is unhelpful or over-permissive. */
export function reviewRoleplay(
  input: Input,
  result: Output & { checks?: z.infer<typeof challengeChecksSchema> },
): Output {
  const { turn } = roleplayContext(input);
  if (input.difficulty === "challenge" && "criteria" in turn)
    return roleplayOutput.parse(reviewChallenge(input, turn, result));
  const text = input.text.toLowerCase().replaceAll("’", "'");
  const friendScene =
    input.scene === "shopping" || input.scene === "amusement_park";
  const blocked =
    instructionsOrHelp.test(text) || (!friendScene && negatedGoal.test(text));
  const onGoal =
    evidence[`${input.scene}/${input.role}`][input.turn].every((rule) =>
      rule.test(normalize(text)),
    ) ||
    (friendScene && naturalChoice.test(normalize(text)));
  const authoredAnswer = normalize(input.text) === normalize(turn.sample);
  const accepted = !blocked && onGoal && (authoredAnswer || result.accepted);
  if (!accepted)
    return roleplayOutput.parse({
      accepted: false,
      correction: turn.sample,
      feedback: `Try saying: ${turn.sample}`,
      acknowledgement: "Let's try that together.",
    });

  // Exact authored answers need no repair. For other answers, keep the model's
  // grammar repair only if it still fulfils the same goal and is text feedback.
  const basicRepair = friendScene
    ? input.text.replace(
        /\b(I|you|we|they)\s+(wants|needs|likes)\b/gi,
        (_match, subject: string, verb: string) =>
          `${subject} ${verb.slice(0, -1)}`,
      )
    : input.text;
  const repair =
    result.correction || (basicRepair !== input.text ? basicRepair : "");
  const originalMeaning = meaningWords(input.text);
  const repairedMeaning = meaningWords(repair);
  const preservesMeaning =
    originalMeaning.length === repairedMeaning.length &&
    originalMeaning.every((word, index) => repairedMeaning[index] === word);
  const usefulRepair =
    repair &&
    !unsupported.test(repair) &&
    !instructionsOrHelp.test(repair.toLowerCase()) &&
    (friendScene
      ? preservesMeaning
      : !negatedGoal.test(repair.toLowerCase())) &&
    (evidence[`${input.scene}/${input.role}`][input.turn].every((rule) =>
      rule.test(normalize(repair)),
    ) ||
      (friendScene && naturalChoice.test(normalize(repair))));
  const dialogue = ` ${normalize([input.text, ...input.history].join(" "))} `;
  const ungroundedChoices = (message: string) =>
    friendScene &&
    (normalize(turn.sample).match(sampleChoices) ?? []).some(
      (choice) =>
        ` ${normalize(message)} `.includes(` ${choice} `) &&
        !dialogue.includes(` ${choice} `),
    );
  const acknowledgement = result.acknowledgement
    .replace(/[^.!?]*\?/g, "")
    .trim();
  return roleplayOutput.parse({
    accepted: true,
    correction:
      authoredAnswer || !repair || normalize(input.text) === normalize(repair)
        ? ""
        : usefulRepair
          ? repair
          : "",
    feedback:
      !result.accepted ||
      unsupported.test(result.feedback) ||
      ungroundedChoices(result.feedback) ||
      (repair && !usefulRepair)
        ? "Nice, that works."
        : result.feedback,
    acknowledgement:
      !result.accepted ||
      !acknowledgement ||
      unsupported.test(acknowledgement) ||
      ungroundedChoices(acknowledgement)
        ? "Sounds good."
        : acknowledgement,
  });
}
