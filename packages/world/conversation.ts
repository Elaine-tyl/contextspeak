import { z } from "zod";

export const QUESTIONS = [
  "What do you like to drink, and why?",
  "What would you like to do this weekend?",
  "Could you invite me to join you and suggest a time?",
] as const;

export const conversationInput = z
  .strictObject({
    text: z.string().trim().min(1).max(400),
    turn: z.number().int().min(0).max(2),
    order: z.string().max(80),
    history: z
      .array(
        z.strictObject({
          text: z.string().min(1).max(400),
          feedback: z.string().max(240),
        }),
      )
      .max(2),
  })
  .refine(
    (input) => input.history.length === input.turn,
    "History must match the current turn",
  );

export const conversationOutput = z.strictObject({
  accepted: z.boolean(),
  acknowledgement: z.string().trim().min(1).max(220),
  feedback: z.string().trim().min(1).max(240),
});

const SUCCESS_TIPS = [
  "You gave a preference and a reason. You can add a specific detail about the drink.",
  "Your plan clearly names an activity. You can add where or who you would like to go with.",
  "Your invitation includes a time. A polite pattern is: Would you like to join me?",
];

const TIPS = [
  "Name a drink and give a reason, for example: I like tea because it helps me relax.",
  "Describe a weekend activity with an expression such as: I'd like to visit the park.",
  "Invite your friend and suggest a time, for example: Would you like to meet at ten on Saturday?",
];

/** Clear goal evidence takes precedence over an inconsistent small-model verdict. */
export function reviewConversation(
  input: Pick<z.infer<typeof conversationInput>, "text" | "turn">,
  result: z.infer<typeof conversationOutput>,
) {
  const text = input.text.toLowerCase().replaceAll("’", "'");
  let evidence: boolean | undefined;
  let repairTip = TIPS[input.turn];
  if (
    /ignore.*instructions|system prompt|mark.*accepted|return.*json|what does.*mean|help me|please explain|(?:can|could) you (?:help|explain|repeat)|how (?:do|can) i say/.test(
      text,
    )
  )
    evidence = false;
  else if (
    input.turn === 0 &&
    /\b(tea|coffee|water|juice|milk|cocoa|chocolate|espresso|cappuccino|lemonade)\b/.test(
      text,
    )
  ) {
    repairTip =
      "Add a reason for your drink choice, for example: I like tea because it helps me relax.";
    if (
      /because|helps?|makes?|tastes?|refresh|relax|energ|warm|sweet|healthy|love its|like its/.test(
        text,
      )
    ) {
      evidence = true;
    } else if (
      /^i (?:really )?(?:like|love|prefer|enjoy) (?:drinking )?(?:tea|coffee|water|juice|milk|cocoa|chocolate|espresso|cappuccino|lemonade)[.!]?$/.test(
        text,
      )
    ) {
      evidence = false;
    }
    // Other explanations (for example, "for its rich aroma") stay with the model.
  } else if (
    input.turn === 1 &&
    // Recognize activities in ordinary inflected forms, even in an imperfect sentence.
    /\b(?:go|visit|meet|walk|watch|play|read|study|hik|cook|shop|danc|travel|swim|celebrat)(?:e|s|ed|ing|ping|ming|ling)?\b/.test(
      text,
    )
  ) {
    evidence =
      /would like|i'd like|going to|want to|plan to|\bwill\b|this weekend|on saturday|on sunday/.test(
        text,
      )
        ? true
        : undefined;
  } else if (input.turn === 2) {
    const invitation =
      /would you|do you want|join me|let's|can we|shall we|will you/.test(text);
    const time =
      /\b(at|around|about|by)\s+(\d|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|noon)|\b(saturday|sunday|monday|tuesday|wednesday|thursday|friday|tomorrow|tonight|morning|afternoon)\b/.test(
        text,
      );
    // Explicit evidence can correct a false rejection; other time phrases stay with the model.
    if (invitation && time) evidence = true;
    else if (invitation) {
      repairTip =
        "Add a time to your invitation, for example: at ten on Saturday.";
      // Bare invitations clearly lack a time; unfamiliar time phrases remain model-reviewed.
      if (/(?:join me|come along|go together)\s*[?!.]*$/.test(text))
        evidence = false;
    } else if (time) {
      repairTip =
        "Invite your friend to join you, for example: Would you like to join me at that time?";
      if (!/join|meet|come|go|how about|fancy|free/.test(text))
        evidence = false;
    }
  }
  const accepted = evidence ?? result.accepted;
  const acknowledgement =
    result.acknowledgement
      .replace(/\p{Extended_Pictographic}|\uFE0F/gu, "")
      .replace(/[^.!?]*\?/g, "")
      .trim() || "Thanks for sharing.";
  const unsupported =
    /pronunciation|accent|phonetic|pause|intonation|rhythm|speak slower/.test(
      result.feedback.toLowerCase(),
    );
  return conversationOutput.parse({
    accepted,
    acknowledgement,
    feedback: !accepted
      ? repairTip
      : unsupported || (evidence !== result.accepted && evidence !== undefined)
        ? SUCCESS_TIPS[input.turn]
        : result.feedback,
  });
}
