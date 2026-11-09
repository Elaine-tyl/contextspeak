import { createHash } from "node:crypto";
import { z } from "zod";
const licenses = {
  "CC0-1.0": "https://creativecommons.org/publicdomain/zero/1.0/",
  "CC-BY-3.0": "https://creativecommons.org/licenses/by/3.0/",
  "Public-domain-VOA": "https://learningenglish.voanews.com/p/6861.html",
} as const;
const sourceSchema = z
  .object({
    id: z.string().regex(/^[a-z0-9-]+$/),
    provider: z.enum(["Tatoeba", "VOA Learning English", "African Storybook"]),
    title: z.string().trim().min(1).max(160),
    url: z.url(),
    author: z.string().trim().min(1).max(200),
    license: z.enum(["CC0-1.0", "CC-BY-3.0", "Public-domain-VOA"]),
    licenseUrl: z.url(),
    fetchUrl: z.url(),
  })
  .strict();
const excerptSchema = z
  .object({
    id: z.string().regex(/^[a-z0-9-]+$/),
    sourceId: z.string(),
    text: z.string().trim().min(1).max(1200),
    level: z.number().int().min(1).max(10),
    topic: z.string().trim().min(1).max(40),
    question: z.string().trim().min(1).max(300),
    sha256: z.string().regex(/^[a-f0-9]{64}$/),
  })
  .strict();
export type ContentSource = z.infer<typeof sourceSchema>;
export type ContentExcerpt = z.infer<typeof excerptSchema>;
export function textDigest(text: string) {
  return createHash("sha256").update(text).digest("hex");
}
function sourceMatchesPolicy(source: ContentSource) {
  if (source.provider === "Tatoeba") {
    const id = source.id.replace("tatoeba-", "");
    return (
      /^tatoeba-\d+$/.test(source.id) &&
      source.license === "CC0-1.0" &&
      source.fetchUrl === `https://api.tatoeba.org/v1/sentences/${id}` &&
      source.url === `https://tatoeba.org/en/sentences/show/${id}`
    );
  }
  if (source.provider === "African Storybook") {
    const pinnedFile =
      /^https:\/\/raw\.githubusercontent\.com\/global-asp\/asp-source\/[a-f0-9]{40}\/en\/\d{4}_[a-z0-9-]+\.md$/;
    const creditedFile = source.fetchUrl
      .replace("raw.githubusercontent.com", "github.com")
      .replace("asp-source/", "asp-source/blob/");
    return (
      pinnedFile.test(source.fetchUrl) &&
      source.license === "CC-BY-3.0" &&
      source.url === creditedFile
    );
  }
  const url = new URL(source.fetchUrl);
  return (
    url.origin === "https://learningenglish.voanews.com" &&
    /^\/a\/lets-learn-english-.*\/\d+\.html$/.test(url.pathname) &&
    source.license === "Public-domain-VOA" &&
    source.url === source.fetchUrl &&
    source.author === "Voice of America"
  );
}
export function validateCorpus(input: unknown) {
  const corpus = z
    .object({
      version: z.literal(1),
      reviewedAt: z.iso.date(),
      sources: z.array(sourceSchema).min(1),
      excerpts: z.array(excerptSchema).min(1),
    })
    .strict()
    .parse(input);
  const sources = new Map<string, ContentSource>();
  for (const source of corpus.sources) {
    const expected = sourceMatchesPolicy(source);
    if (
      !expected ||
      source.licenseUrl !== licenses[source.license] ||
      sources.has(source.id)
    )
      throw new Error(`Unreviewed source: ${source.id}`);
    sources.set(source.id, source);
  }
  const ids = new Set<string>();
  const texts = new Set<string>();
  for (const excerpt of corpus.excerpts) {
    const normalized = normalizeText(excerpt.text).toLowerCase();
    if (
      !sources.has(excerpt.sourceId) ||
      ids.has(excerpt.id) ||
      texts.has(normalized) ||
      excerpt.sha256 !== textDigest(excerpt.text)
    )
      throw new Error(`Invalid or duplicate excerpt: ${excerpt.id}`);
    ids.add(excerpt.id);
    texts.add(normalized);
  }
  return corpus;
}
function normalizeText(text: string) {
  return text
    .normalize("NFKC")
    .replace(/[‘’]/g, "'")
    .replace(/[“”]/g, '"')
    .replace(/\s+/g, " ")
    .trim();
}
function htmlText(html: string) {
  const entities: Record<string, string> = {
    amp: "&",
    quot: '"',
    apos: "'",
    nbsp: " ",
    rsquo: "’",
    lsquo: "‘",
    ldquo: "“",
    rdquo: "”",
  };
  return html
    .replace(/<(script|style)\b[^>]*>[\s\S]*?<\/\1>/gi, " ")
    .replace(/<[^>]*>/g, " ")
    .replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (match, entity: string) => {
      if (!entity.startsWith("#")) return entities[entity] ?? match;
      const code =
        entity[1].toLowerCase() === "x"
          ? parseInt(entity.slice(2), 16)
          : Number(entity.slice(1));
      return code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : match;
    });
}
export function verifySourceText(
  source: ContentSource,
  excerpts: ContentExcerpt[],
  body: string,
) {
  let text = body;
  if (source.provider === "Tatoeba") {
    const parsed = JSON.parse(body);
    const sentence = parsed.data ?? parsed;
    if (
      sentence.id !== Number(source.id.replace("tatoeba-", "")) ||
      sentence.lang !== "eng" ||
      sentence.license !== "CC0 1.0" ||
      sentence.is_unapproved !== false ||
      sentence.owner !== source.author
    )
      throw new Error(`Changed Tatoeba metadata: ${source.id}`);
    text = sentence.text;
  } else if (source.provider === "VOA Learning English") text = htmlText(body);
  else {
    if (
      !body.includes("* License: [CC-BY]") ||
      !body.includes(`* Text: ${source.author}\n`)
    )
      throw new Error(`Changed story attribution: ${source.id}`);
    text = body.replace(/^##[ \t]*$/gm, " ");
  }
  const normalized = normalizeText(text);
  for (const excerpt of excerpts) {
    if (!normalized.includes(normalizeText(excerpt.text)))
      throw new Error(`Source text changed: ${excerpt.id}`);
  }
}
