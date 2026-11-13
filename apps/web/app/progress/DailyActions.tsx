import Link from "next/link";
import type { LearningProgress } from "@contextspeak/contracts";
import { practiceEntry, dailyExpression } from "../../src/learning-plan";
export function DailyActions({ data }: { data: LearningProgress }) {
  const expression = dailyExpression(data.personalization)?.word;
  const actions = [
    {
      step: "01",
      title: "Review",
      copy: data.vocabulary.due
        ? `${data.vocabulary.due} words ready for recall`
        : "Keep your vocabulary fresh",
      label: data.vocabulary.due ? "Review now" : "Open review queue",
      href: "/library?view=reviews",
    },
    {
      step: "02",
      title: "Listen",
      copy: "Hear one saved expression. Write what you hear.",
      label: "Start listening",
      href: practiceEntry(data.personalization, "listening"),
    },
    {
      step: "03",
      title: "Speak",
      copy: "Answer aloud and get feedback on your response.",
      label: "Start speaking",
      href: practiceEntry(data.personalization, "speaking"),
    },
  ];
  return (
    <section className="daily-actions" aria-label="Your next steps">
      {actions.map((action) => (
        <article
          data-depth
          key={action.step}
          className={
            action.step === "01"
              ? "tone-review"
              : action.step === "02"
                ? "tone-speak"
                : "tone-brand"
          }
        >
          <p className="daily-step">
            <span aria-hidden="true">{Number(action.step)}</span>
            {action.title}
          </p>
          <p>{action.copy}</p>
          {action.step !== "01" && (
            <p className="daily-expression">
              {expression
                ? `Using “${expression}”`
                : "Choose a short expression in your library."}
            </p>
          )}
          <Link
            className={
              action.step === "01" && data.vocabulary.due
                ? "primary"
                : "secondary"
            }
            href={action.href}
          >
            {action.label}
          </Link>
        </article>
      ))}
    </section>
  );
}
