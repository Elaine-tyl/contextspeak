import { conversationScenarios } from "@contextspeak/contracts";
import { learningReturn, practiceMode } from "../../../src/navigation";
import { PracticePage } from "../PracticePage";
export default async function Page({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{
    lookup?: string | string[];
    scenario?: string | string[];
    returnTo?: string | string[];
    mode?: string | string[];
  }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const scenarioId = conversationScenarios.find(
    (value) => value.id === query.scenario,
  )?.id;
  const lookupId = typeof query.lookup === "string" ? query.lookup : undefined;
  return (
    <PracticePage
      key={`${id}:${lookupId ?? "latest"}:${scenarioId ?? "example"}:${practiceMode(query.mode, Boolean(scenarioId))}`}
      id={id}
      returnTo={learningReturn(query.returnTo)}
      initialMode={practiceMode(query.mode, Boolean(scenarioId))}
      scenarioId={scenarioId}
      lookupId={lookupId}
    />
  );
}
