import { downloadSource } from "../apps/api/src/content/download";
import corpus from "../apps/api/src/content/corpus.json";
import {
  validateCorpus,
  verifySourceText,
} from "../apps/api/src/content/validation";
const reviewed = validateCorpus(corpus);
if (process.argv.includes("--online")) {
  // Explicit maintenance only: no credentials, bulk crawling or runtime scraping.
  for (const source of reviewed.sources) {
    const body = await downloadSource(source.fetchUrl);
    verifySourceText(
      source,
      reviewed.excerpts.filter((excerpt) => excerpt.sourceId === source.id),
      body,
    );
    console.log(`Verified ${source.id}`);
  }
}
console.log(
  `Validated ${reviewed.excerpts.length} reviewed excerpts from ${reviewed.sources.length} source documents. Practice works from the committed snapshot without internet fetching.`,
);
