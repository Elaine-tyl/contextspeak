import { z } from "zod";
import catalog from "../../games/unity/Assets/Resources/LifeCatalog.json" with { type: "json" };
import {
  normalizeRoleplayText,
  challengeChecksFor,
  roleplayMeaningWords,
  roleplayInstructionsOrHelp,
  unsupportedRoleplayFeedback,
} from "./challenges";

const label = z.string().trim().min(1).max(240);
const catalogSchema = z
  .strictObject({
    items: z
      .array(
        z.strictObject({
          id: z.string().regex(/^[a-z_]+$/),
          name: label,
          price: z.number().int().min(1).max(2000),
          category: z.enum(["produce", "drink"]),
        }),
      )
      .length(6),
    npcs: z
      .array(
        z.strictObject({
          id: label,
          name: label,
          role: label,
          place: label,
          x: z.number().min(-20).max(20),
          z: z.number().min(-22).max(14),
          standX: z.number().min(-20).max(20),
          standZ: z.number().min(-22).max(14),
        }),
      )
      .length(4),
    steps: z
      .array(
        z.strictObject({
          id: label,
          npc: label,
          question: label,
          goal: label,
          sample: label,
          criteria: z.array(label).max(2).default([]),
        }),
      )
      .length(12),
  })
  .refine(
    (data) =>
      [data.items, data.npcs, data.steps].every(
        (rows) => new Set(rows.map((row) => row.id)).size === rows.length,
      ) &&
      data.steps.every((step) => data.npcs.some((npc) => npc.id === step.npc)),
    "Use unique IDs and known NPCs",
  );

export const LIFE_CATALOG = catalogSchema.parse(catalog);
export const lifeInput = z.strictObject({
  step: z
    .string()
    .refine((id) => LIFE_CATALOG.steps.some((step) => step.id === id)),
  text: z.string().trim().min(1).max(400),
  repair: z.string().trim().max(240).default(""),
  drink: z.enum(["", "coffee", "latte", "tea", "lemonade"]).default(""),
});
export const lifeOutput = z.strictObject({
  accepted: z.boolean(),
  correction: z.string().trim().max(240),
  feedback: label,
  acknowledgement: z.string().trim().min(1).max(160),
  choice: z.enum(["", "coffee", "latte", "tea", "lemonade"]),
});
export type LifeInput = z.infer<typeof lifeInput>;
export type LifeReview = z.infer<typeof lifeOutput>;
export type LifeAssessment = LifeReview & {
  checks?: { met: boolean; quote: string }[];
};
export const lifeModelOutputFor = (input: LifeInput) =>
  input.step.startsWith("friend_")
    ? lifeOutput.extend({ checks: challengeChecksFor(input.text) })
    : lifeOutput;
export const lifeContext = (input: LifeInput) =>
  LIFE_CATALOG.steps.find((step) => step.id === input.step)!;

const normalize = normalizeRoleplayText;
const selections: Record<string, string[]> = {
  market_pay: ["cash", "card"],
  cafe_order: ["coffee", "latte", "tea", "lemonade"],
  cafe_temperature: ["hot", "iced"],
  cafe_service: ["for here", "to go"],
};
function positiveChoice(step: string, text: string) {
  const choices = selections[step];
  if (!choices) return "";
  const words = normalize(text)
    .replace(/\bcold\b/g, "iced")
    .replace(/\b(?:take away|takeaway)\b/g, "to go");
  const options = choices.join("|");
  const positive = words.replace(
    new RegExp(
      `\\b(?:not|no|don't want|do not want|dont want) (?:a |an |the )?(?:${options})\\b`,
      "g",
    ),
    "",
  );
  if (
    /\b(?:don't|do not|not|never|cannot|can't) (?:want|pay|paying|order|buy|have|take)\b/.test(
      positive,
    )
  )
    return "";
  const found = choices.filter((choice) =>
    new RegExp(`\\b${choice}\\b`).test(positive),
  );
  return found.length === 1 ? found[0] : "";
}
const negation =
  /\b(?:(?:do not|don't|dont|never|won't|will not|cannot|can't) (?:want|buy|get|pick|pay|bring|have|take|order|help|confirm)|not (?:three|3|two|2|hot|iced|for here|to go))\b/;
const goals: Record<string, RegExp[]> = {
  home_errand: [
    /\b(?:sure|yes|yeah|yep|okay|ok|will|can|happy to|no problem|absolutely|certainly|of course|consider it done|you bet|sounds good)\b/,
  ],
  market_apples: [/\b(?:three|3) apples?\b/],
  market_carrots: [/\b(?:two|2) carrots?\b/],
  market_pay: [/\b(?:cash|card)\b/],
  home_deliver: [
    /\bapples?\b/,
    /\bcarrots?\b/,
    /\b(?:here|got|brought|bought|have|your)\b/,
  ],
  cafe_order: [/\b(?:coffee|latte|tea|lemonade)\b/],
  cafe_temperature: [/\b(?:hot|iced|cold)\b/],
  cafe_service: [/\b(?:for here|to go|takeaway|take away)\b/],
  cafe_confirm: [
    /^(?:yes|yeah|yep|correct|that is correct|that's correct|go ahead|sounds good)\b/,
  ],
  friend_preference: [/\S+\s+\S+\s+\S+/],
  friend_plan: [],
  friend_invite: [
    /\b(?:want to|would you|can you|could you|join|come|let's|lets|let us|how about|what about|care to|are you free|you in)\b/,
  ],
};
function meetsGoal(step: string, text: string) {
  if (roleplayInstructionsOrHelp.test(normalize(text))) return false;
  if (selections[step])
    return (
      positiveChoice(step, text) !== "" &&
      (step !== "cafe_order" ||
        !/\b(?:[2-9]|[0-9]{2,}|coffees|lattes|teas|lemonades)\b/.test(
          normalize(text),
        ))
    );
  const words = normalize(text);
  if (!step.startsWith("friend_") && negation.test(words)) return false;
  if (
    step === "cafe_confirm" &&
    /\b(?:no|not|isn't|don't|but|wait|maybe|actually|unless|instead|if|guess|unsure|later|cancel|stop|hold|change|wrong|incorrect)\b/.test(
      text.toLowerCase(),
    )
  )
    return false;
  if (
    step.startsWith("friend_") &&
    /^(?:yes|no|okay|ok|sure|thanks|thank you|i don't know)$/.test(words)
  )
    return false;
  return goals[step].every((rule) => rule.test(words));
}

/** Only language evidence leaves this boundary; game state/actions stay in C#. */
export function reviewLife(
  input: LifeInput,
  result: LifeAssessment,
): LifeReview {
  const context = lifeContext(input);
  const selected = positiveChoice(input.step, input.text);
  const sample =
    input.step === "cafe_order" && selected
      ? `Could I get a ${selected}, please?`
      : context.sample;
  const retry = () => ({
    accepted: false,
    choice: "" as const,
    correction: sample,
    feedback: `Try saying: ${sample}`,
    acknowledgement: "Let's try that together.",
  });
  if (!meetsGoal(input.step, input.text)) return retry();
  if (
    input.step.startsWith("friend_") &&
    (result.checks?.length !== 2 ||
      result.checks.some(
        (check) =>
          !check.met || !check.quote || !input.text.includes(check.quote),
      ))
  )
    return retry();
  const authored = normalize(input.text) === normalize(context.sample);
  const sameMeaning =
    roleplayMeaningWords(input.text).join(" ") ===
    roleplayMeaningWords(result.correction).join(" ");
  const pluralRepair =
    /^([2-9]) (apple|carrot)\b/.test(normalize(input.text)) &&
    result.correction &&
    /^([2-9]) (apples|carrots)\b/.test(normalize(result.correction));
  const usefulRepair =
    result.correction &&
    (sameMeaning || pluralRepair) &&
    meetsGoal(input.step, result.correction) &&
    !unsupportedRoleplayFeedback.test(result.correction);
  if (!result.accepted && !authored)
    return usefulRepair
      ? {
          accepted: false,
          correction: result.correction,
          acknowledgement: "Let's try that together.",
          choice: "",
          feedback: unsupportedRoleplayFeedback.test(result.feedback)
            ? "Try the corrected wording."
            : result.feedback,
        }
      : retry();
  if (
    input.step === "cafe_order" &&
    (!result.choice || result.choice !== selected)
  )
    return retry();
  const shortOrder =
    /^(?:(?:[2-9] (?:apples|carrots))|(?:1 |a |an )?(?:apple|carrot|coffee|latte|tea|lemonade))(?: please| thanks)?$/.test(
      normalize(input.text),
    );
  const changed =
    !authored &&
    !shortOrder &&
    result.correction &&
    normalize(result.correction) !== normalize(input.text);
  if (changed && !usefulRepair) return retry();
  return {
    accepted: true,
    correction: changed ? result.correction : "",
    choice: input.step === "cafe_order" ? result.choice : "",
    feedback: unsupportedRoleplayFeedback.test(result.feedback)
      ? "Your meaning is clear."
      : result.feedback,
    acknowledgement: unsupportedRoleplayFeedback.test(result.acknowledgement)
      ? "Got it."
      : result.acknowledgement.replace(/[^.!?]*\?/g, "").trim() || "Got it.",
  };
}
