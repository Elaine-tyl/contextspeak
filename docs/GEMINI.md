# Gemini explanations

Gemini is an optional cloud provider for tutor-style definitions, common meanings, usage distinctions and bilingual examples. Selection stays automatic and collected lookups still have one shared stored result.

## Server configuration

Create an API key restricted to `generativelanguage.googleapis.com` in your own Google project. Keep the project on its unpaid/free tier for free-tier usage. Do not enable billing unless you explicitly intend to pay. Store only on the server in ignored `.env.local` (mode 600):

```dotenv
TRANSLATION_PROVIDER=gemini
GEMINI_API_KEY=your_key_here
```

Restart `pnpm dev`, reload the unpacked extension (v0.7.1), and refresh webpages. Keep automatic translation enabled. Supported languages: English, Chinese, Japanese, Korean, Spanish, French, Vietnamese. AI selections accept up to 600 Unicode characters.

The adapter uses the official Gemini Developer API over HTTPS with `gemini-3.1-flash-lite`, a model currently listed with free-tier text input/output in Google's pricing. It sends the key in `x-goog-api-key`, never in a URL or browser bundle. Only selected text and a bounded sanitized excerpt are sent; page URLs are omitted. Cloud processing differs from local Ollama: unpaid-service prompts/responses may be used by Google to improve products under its terms.

## Repeated translations

Successful Gemini cards are cached on the backend for 24 hours, up to 500 entries total, with least-recently-used eviction. The cache is isolated per authenticated user and matches exact selected text, context, source language and target language. Page URLs are excluded because Gemini does not receive them. Concurrent identical requests share one generation; collected and transient lookups use the same cache. Each collected encounter still has its own saved record and count. Failures are never cached.

The cache contains generated cards in memory, including transient results; it does not create vocabulary records when collection is off. Restarting the server clears it and invalidates old model/prompt results. This is a single-process cache, not a distributed production cache.

Cache hits do not consume Gemini requests, even when the local cap or Google quota is exhausted. Uncached requests can still fail with a quota message. Existing vocabulary and learning notes remain readable from the database without calling Gemini; Retry updates the failed lookup instead of creating another encounter. There is no automatic retry or paid fallback.

## Quotas and reliability

Free availability and quotas depend on your account, project and model; see AI Studio. The app makes at most five Gemini requests per minute per server process as a conservative local cap. This cap is not a billing control and does not prove that a paid project is free. Keep billing disabled for the free-tier setup. HTTP 429 shows a quota message; there are no automatic retries or fallback to another paid model. Resetting/restarting the server resets the local cap, while Google's project quotas remain authoritative.

JSON-schema structured output is locally validated with Zod, including exact selected scope, target language, shape, example count and size. Blocked or incomplete responses remain failed saved lookups with Retry available. Generated notes are rendered as text. Cloud model revisions are recorded when Google supplies `modelVersion`; model-weight digests are unavailable and stored as null rather than fabricated.

## Checks

`pnpm test` includes fake-key Gemini transport tests and a temporary-database integration test proving that the history and result endpoint share one explanation and enforce ownership. These tests never call Google.

`pnpm test:gemini:live` is an opt-in browser acceptance test using the real server-side key and public sample text in a temporary database. Stop the normal development server first because the test uses the same ports. It verifies automatic word/sentence translation, explanations/examples and the matching library record. This live flow passed on 8 November 2026; the project was shown as Free tier and the key restricted to Gemini API. Credentials are never committed. Local Ollama remains available explicitly with `TRANSLATION_PROVIDER=ollama`; the plain translator uses `libretranslate`.

Sources: [API keys](https://ai.google.dev/gemini-api/docs/api-key), [structured output](https://ai.google.dev/gemini-api/docs/structured-output), [pricing](https://ai.google.dev/gemini-api/docs/pricing), [terms](https://ai.google.dev/gemini-api/terms).

## Compact card and library filters

Extension v0.5.0 keeps selection translation automatic. The card starts compact; use **Show explanation** to expand notes/examples and **Hide explanation** to collapse them. **Close** or Escape dismisses the card without deleting or cancelling an already-started save. A later response never reopens a dismissed card.

Save indicators distinguish **Saved locally · waiting for sync**, **Saved to library**, and **Not saved · collection off or site excluded**. A translation error does not imply the lookup was lost. Retrying an owned persisted lookup retains its saved indicator, including while offline.

The library toolbar searches the full owned history by selected text, context or translated meaning; candidate search matches selected text. Source and target filters run before pagination, and source filters include detected languages. Filtering does not call Gemini or modify saved data. Changing filters resets pagination; Clear filters restores the default list. Queries are limited to 200 characters.

## AI pronunciation

Optional `SPEECH_PROVIDER=gemini` enables natural word, phrase and example audio using the existing server-side key. See [AI speech setup, privacy and cache limits](AI_SPEECH.md). Billing is not enabled by this code; user recordings remain local.
