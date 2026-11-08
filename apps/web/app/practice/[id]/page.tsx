import { conversationScenarios } from "@contextspeak/contracts";
import { PracticePage } from "../PracticePage";
export default async function Page({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{
    lookup?: string | string[];
    scenario?: string | string[];
  }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  const scenarioId = conversationScenarios.find(
    (value) => value.id === query.scenario,
  )?.id;
  const lookupId = typeof query.lookup === "string" ? query.lookup : undefined;
  return (
    <PracticePage
      key={`${id}:${lookupId ?? "latest"}:${scenarioId ?? "example"}`}
      id={id}
      scenarioId={scenarioId}
      lookupId={lookupId}
    />
  );
}
