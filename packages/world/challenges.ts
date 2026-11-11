import { z } from "zod";
import everydayCatalog from "../../games/cafe/data/roleplay.json";
import challengeCatalog from "../../games/cafe/data/real_life.json";

export const challengeSceneId = z.enum([
  "shopping",
  "farm",
  "market",
  "restaurant",
  "amusement_park",
  "cafe",
]);
const line = z.string().trim().min(1).max(240);
const criteria = z.tuple([
  z.string().trim().min(1).max(160),
  z.string().trim().min(1).max(160),
]);
const exchangeSchema = z.strictObject({
  prompt: line,
  goal: line,
  sample: line,
  criteria,
});
const turnSchema = exchangeSchema.extend({
  npc: z.strictObject({
    id: z.string().trim().min(1).max(40),
    name: z.string().trim().min(1).max(80),
    role: z.string().trim().min(1).max(80),
  }),
  action: z.string().min(1).max(40),
  follow_up: exchangeSchema,
});

/** Challenges share walking routes with Everyday; cafe deliberately reuses Maya. */
export const challengeCatalogSchema = z
  .array(
    z
      .strictObject({
        id: challengeSceneId,
        title: z.string().trim().min(1).max(80),
        roles: z
          .array(
            z.strictObject({
              id: z.string().min(1).max(40),
              label: z.string().trim().min(1).max(80),
              npc: z.string().trim().min(1).max(80),
              setting: z.string().trim().min(1).max(400),
              turns: z.array(turnSchema).length(3),
            }),
          )
          .min(1)
          .max(2),
      })
      .superRefine((scene, context) => {
        const base = everydayCatalog.find((entry) => entry.id === scene.id);
        const roleIds = base ? base.roles.map((role) => role.id) : ["customer"];
        if (
          scene.roles.length !== roleIds.length ||
          new Set(scene.roles.map((role) => role.id)).size !== roleIds.length
        )
          context.addIssue({
            code: "custom",
            path: ["roles"],
            message: "Use each authored role exactly once",
          });
        scene.roles.forEach((role, roleIndex) => {
          if (!roleIds.includes(role.id))
            context.addIssue({
              code: "custom",
              path: ["roles", roleIndex, "id"],
              message: "Choose a role from this scene",
            });
          const baseRole = base?.roles.find((entry) => entry.id === role.id);
          role.turns.forEach((turn, turnIndex) => {
            const expected = baseRole?.turns[turnIndex];
            const id =
              expected?.npc.id ?? ["counter", "counter", "friend"][turnIndex];
            const action =
              expected?.action ?? ["basket", "pay", "table"][turnIndex];
            if (turn.npc.id !== id || turn.action !== action)
              context.addIssue({
                code: "custom",
                path: ["roles", roleIndex, "turns", turnIndex],
                message: "NPC and action must match the authored scene route",
              });
            if (
              scene.id === "cafe" &&
              turn.npc.name !== ["Maya", "Maya", "Alex"][turnIndex]
            )
              context.addIssue({
                code: "custom",
                path: ["roles", roleIndex, "turns", turnIndex, "npc", "name"],
                message: "Use the cafe's existing characters",
              });
          });
        });
      }),
  )
  .length(6)
  .refine(
    (scenes) => new Set(scenes.map((scene) => scene.id)).size === 6,
    "Scene IDs must be unique",
  );

export const CHALLENGE_SCENES = challengeCatalogSchema.parse(challengeCatalog);
const check = z.strictObject({
  met: z.boolean(),
  quote: z.string().trim().max(400),
});
export const challengeChecksSchema = z.array(check).length(2);

/** Give the model a small menu of exact evidence, never generated quotations. */
export function challengeChecksFor(text: string) {
  const reply = text.trim();
  const sentences = reply.split(/(?<=[.!?])\s+|\n+/).map((part) => part.trim());
  const clauses = sentences.flatMap((sentence) =>
    sentence
      .split(/(?<=[,;])\s+|\s+(?=(?:because|but|and|so)\b)/i)
      .map((part) => part.trim()),
  );
  const candidates = [...new Set(["", reply, ...sentences, ...clauses])].slice(
    0,
    8,
  );
  return z.array(check.extend({ quote: z.enum(candidates) })).length(2);
}

export const roleplayInstructionsOrHelp =
  /(?:ignore|disregard|forget).{0,40}(?:instruction|previous|above|rules?)|system\s*(?:prompt|message)|\b(?:assistant|developer)\s*:|<\/?(?:system|assistant)|\b(?:mark|set|return|output).{0,40}(?:accepted|json|complete|success|true)|\b(?:skip|unlock).{0,25}(?:turn|task|level)|\bhelp me\b|\b(?:can|could|please) you (?:help|explain|repeat)|\b(?:what should i say|what does .+ mean|how (?:do|can) i say)\b/;
// Keep the legacy Everyday guard above. Challenges judge practical assistance
// and clarification by their criteria; only explicit coaching/control is blocked.
const challengeMetaPatterns = [
  /\b(?:ignore|disregard|forget)\s+(?:(?:all|any|the|your|previous|above|system|developer)\s+){0,4}(?:instructions?|rules?|prompts?)\b/,
  /\b(?:system|developer|assistant)\s*(?::|prompt\b|message\b)|<\/?(?:system|developer|assistant)\b/,
  /\b(?:mark|set)\s+(?:(?:this|my|the|your)\s+)?(?:(?:reply|answer|response|result|accepted)\s+)?(?:(?:as|to)\s+)?(?:accepted|complete|success|true)\b|\b(?:return|output)\s+(?:only\s+)?(?:json|true|accepted|success)\b|\baccepted\s*(?::|=|to)?\s*true\b/,
  /\b(?:skip|unlock)\s+(?:(?:the|this|current|next)\s+){0,2}(?:turn|question|level)\b/,
  /\b(?:what should i|how (?:do|can|should) i)\s+(?:say|answer|respond)\b|\btell me what to\s+(?:say|answer)\b|\b(?:answer|write|say)\s+(?:this|that|it)\s+for me\b/,
  /\b(?:explain|repeat|translate)\s+(?:(?:this|the|my|your|current)\s+){0,2}(?:question|prompt|exercise)\b|\bwhat does\s+(?:(?:this|the|current)\s+){0,2}(?:question|prompt|exercise)\s+mean\b/,
  /\bhelp me\s+(?:(?:to\s+)?(?:answer|understand|translate)\s+|with\s+)(?:(?:this|the|my|current)\s+){0,2}(?:question|prompt|exercise)\b|^(?:(?:please|can you|could you)\s+){0,2}help(?: me)?[.!?\s]*$/,
];
const challengeMetaOrControl = (text: string) =>
  challengeMetaPatterns.some((pattern) =>
    pattern.test(text.toLowerCase().replace(/\s+/g, " ")),
  );
export const unsupportedRoleplayFeedback =
  /pronunciat|accent|phonetic|intonation|rhythm|pacing|pauses?|speak slower|spoke|sounded|voice|fluen[ct]/i;
const numbers: Record<string, string> = {
  one: "1",
  two: "2",
  three: "3",
  four: "4",
  five: "5",
  six: "6",
  seven: "7",
  eight: "8",
  nine: "9",
  ten: "10",
  eleven: "11",
  twelve: "12",
};
export const normalizeRoleplayText = (text: string) =>
  text
    .toLowerCase()
    .replaceAll("’", "'")
    .replace(/\b(i|you|we|they)'d\b/g, "$1 would")
    .replace(/\b(i|you|we|they|he|she)'ll\b/g, "$1 will")
    .replace(/\bi'm\b/g, "i am")
    .replace(/\$(\d+)\b/g, "$1 dollars")
    .replace(
      /\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve)\b/g,
      (word) => numbers[word],
    )
    .replace(/[^a-z0-9\s']/g, " ")
    .replace(/\s+/g, " ")
    .trim();

// Compare content words in order: a grammar repair must retain choices,
// quantities, negation and who does what, while allowing function-word repairs.
export const roleplayMeaningWords = (text: string) =>
  normalizeRoleplayText(text)
    .replace(/\bdon't\b/g, "do not")
    .replace(/\bcan't\b/g, "can not")
    .replace(/\bwon't\b/g, "will not")
    .replace(/\b(want|need|like|look)s\b/g, "$1")
    .replace(/\btries\b/g, "try")
    .split(" ")
    .filter(
      (word) =>
        !/^(?:a|an|the|am|is|are|was|were|be|been|being|do|does|did|have|has|had|can|could|may|might|shall|should|will|would|to|for|of|at|in|on|by|from|with|and|or|some|any|please)$/.test(
          word,
        ),
    );

type Feedback = {
  accepted: boolean;
  correction: string;
  feedback: string;
  acknowledgement: string;
};
type Review = Feedback & { checks?: { met: boolean; quote: string }[] };
const genericWord =
  /^(?:i|we|you|it|that|that's|this|a|an|the|is|are|be|do|can|could|would|will|like|want|need|take|have|sounds?|seems?|looks?|yes|yeah|yep|no|nope|ok|okay|sure|certainly|fine|good|great|all|right|alright|very|really|so|please|thanks|thank|for|me|to|and|of|here|there|then|anything|whatever|think)$/;
const nonsense = /\b(?:asdf|qwer(?:ty)?|zxcv|hjkl)\b|\b([^\W\d_])\1{3,}\b/;
const safeAcknowledgement = (message: string) =>
  message
    .split(/(?<=[.!?])\s+/)
    .filter(
      (sentence) =>
        !sentence.includes("?") &&
        !/^(?:what|where|when|why|how|which|would you|could you|can you|do you|are you|shall we|tell me|let me know)\b/i.test(
          sentence,
        ),
    )
    .join(" ")
    .trim();

/** Evidence supports only the current answer; previous dialogue cannot fill gaps. */
export function reviewChallenge(
  input: { text: string; history: string[] },
  exchange: { sample: string; criteria: readonly string[] },
  result: Review,
): Feedback {
  const text = normalizeRoleplayText(input.text);
  const quoteText = (value: string) =>
    value.toLowerCase().replaceAll("’", "'").replace(/\s+/g, " ").trim();
  const currentReply = quoteText(input.text);
  const words = text.split(" ");
  const blocked =
    challengeMetaOrControl(currentReply) ||
    nonsense.test(text) ||
    words.every((word) => genericWord.test(word)) ||
    !/[a-z]{2}/.test(text) ||
    words.some((word) => /^[b-df-hj-np-tv-xz]{5,}$/.test(word));
  const authored = text === normalizeRoleplayText(exchange.sample);
  const grounded =
    result.checks?.length === 2 &&
    result.checks.every(({ met, quote }) => {
      const evidence = quoteText(quote);
      return (
        met && /[a-z0-9]/.test(evidence) && currentReply.includes(evidence)
      );
    });
  if (blocked || !(authored || (result.accepted && grounded)))
    return {
      accepted: false,
      correction: exchange.sample,
      feedback: "Include both details. Try the example below.",
      acknowledgement: "Let's try that together.",
    };

  const repair = result.correction;
  const originalMeaning = roleplayMeaningWords(input.text);
  const repairedMeaning = roleplayMeaningWords(repair);
  const usefulRepair =
    repair &&
    !unsupportedRoleplayFeedback.test(repair) &&
    !challengeMetaOrControl(repair) &&
    originalMeaning.length === repairedMeaning.length &&
    originalMeaning.every((word, index) => word === repairedMeaning[index]);
  const dialogue = new Set(
    roleplayMeaningWords([input.text, ...input.history].join(" ")),
  );
  const sampleOnly = roleplayMeaningWords(exchange.sample).filter(
    (word) => !genericWord.test(word) && !dialogue.has(word),
  );
  const misleading = (message: string) =>
    unsupportedRoleplayFeedback.test(message) ||
    challengeMetaOrControl(message) ||
    roleplayMeaningWords(message).some((word) => sampleOnly.includes(word));
  const acknowledgement = safeAcknowledgement(result.acknowledgement);
  return {
    accepted: true,
    correction:
      authored || !usefulRepair || text === normalizeRoleplayText(repair)
        ? ""
        : repair,
    feedback:
      !result.accepted ||
      misleading(result.feedback) ||
      (repair && !usefulRepair)
        ? "Nice, that works."
        : result.feedback,
    acknowledgement:
      !result.accepted || !acknowledgement || misleading(acknowledgement)
        ? "Sounds good."
        : acknowledgement,
  };
}
