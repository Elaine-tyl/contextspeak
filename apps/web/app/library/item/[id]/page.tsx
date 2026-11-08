import { VocabularyDetailPage } from "../../VocabularyDetailPage";
export default async function Page({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ lookup?: string | string[] }>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  return (
    <VocabularyDetailPage
      id={id}
      lookupId={typeof query.lookup === "string" ? query.lookup : undefined}
    />
  );
}
