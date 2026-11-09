export function vocabularyQuestion(
  expression: string,
  example: string | undefined,
  level: number,
) {
  const word = expression.slice(0, 120);
  const label =
    expression.length > 120 ? "the saved expression" : `“${expression}”`;
  const requirements = ["Give a relevant answer", `Use ${label} naturally`];
  if (level >= 2)
    requirements.push(
      "Explain one reason and ask a relevant follow-up question",
    );
  if (level >= 3)
    requirements.push("Compare two options and justify your choice");
  let context: string;
  if (
    example &&
    example !== expression &&
    example.trim().split(/\s+/).length >= 4
  )
    context = `You hear: “${example.slice(0, 200)}” What would you say next?`;
  else if (/availab|busy|schedule|appointment|weekend/i.test(word))
    context =
      "A friend wants to meet you this weekend. What time and place would you suggest?";
  else if (/academic|research|study|literature|thesis/i.test(word))
    context =
      "A classmate asks about your studies. What are you working on, and why does it interest you?";
  else if (/help|problem|repair|fix|support/i.test(word))
    context =
      "You need help with an everyday problem. How would you explain it and ask politely for help?";
  else
    context = `Explain one common meaning of ${label}, then give a concrete example of when someone could say it.`;
  const extension =
    level === 1
      ? "Answer in one complete sentence."
      : level === 2
        ? "Give a reason and ask one follow-up question."
        : "Compare two possible replies, justify your choice and ask one follow-up question.";
  return {
    prompt: `${context} ${extension} Use ${label}.`,
    criteria: requirements,
  };
}
