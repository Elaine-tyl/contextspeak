# Local AI translation and examples

ContextSpeak's default translator is now local Ollama with the pinned `qwen3:4b` model. It supports English, Chinese, Japanese, Korean, Spanish, and French. No paid API or cloud model is used. Ollama and its model files are upstream dependencies; no upstream application code is copied into ContextSpeak.

## Setup

Use Node 24 and the pinned pnpm version in package.json. Install Ollama from https://ollama.com and start it once, then:

```sh
pnpm install --frozen-lockfile
pnpm db:generate
pnpm ai:setup
pnpm dev
```

The official model download is about 2.5 GB. Setup verifies digest `359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7` and refuses a silently changed model. Dev reuses a running loopback Ollama or starts the installed binary automatically; it stops only a process it owns. Google credentials remain in ignored .env.local. `pnpm dev:local` uses simulated login for local development instead of Google.

Default `TRANSLATION_PROVIDER=ollama`; optional `OLLAMA_URL` must be HTTP loopback. Normally omit it so dev can manage startup. `TRANSLATION_PROVIDER=libretranslate` retains the plain English/Chinese translator documented in TRANSLATION.md; `fixture` is only for deterministic tests. Provider choices are explicit, with no misleading silent fallback.

## Selection behavior

Automatic translation defaults on. Existing explicit automatic-off preferences remain respected; re-enable the setting in the extension if needed. Refresh existing webpages after reloading the extension. After selection, a 300 ms debounce starts translation without pressing a button. The AI card has only a Retry action after failure. Changing source/target on an active card automatically translates with the new pair.

AI selections are limited to **600 Unicode characters** and the card explains when a shorter passage is needed. The plain translator retains its 2,000-character limit. Local AI output is bounded to 4,096 tokens and inference is serialized with a maximum of four queued/running requests. Queue time counts toward the 90-second request deadline. The selection card waits up to two minutes for a saved result; the lookup remains available in the library if the panel is closed.

Words and phrases include a contextual explanation, optional word class, up to two common phrases, and two bilingual usage examples. Sentences include translation, a brief explanation, and one new bilingual example. UI labels stay English; learning explanations and translated examples use the target language. No pronunciation is fabricated. AI-generated notes can still contain linguistic errors; they are labeled accordingly and do not establish vocabulary mastery.

## One result, saved once

Collection remains a separate explicit consent setting. When enabled, the lookup is saved immediately before model generation. The worker stores one versioned `learningDetails` JSON object beside its meaning. The selection card polls that result and the library renders the same data. Retrying a saved AI failure reuses its existing lookup instead of adding an encounter. When collection is off/excluded, translation remains transient.

Only selected text and a bounded sanitized excerpt go to the local model. The URL and full page are omitted. Model output is schema-validated and rendered as text; selected text is treated as data, not instructions. The model digest and prompt/schema version accompany stored details. Old records with no learning JSON remain readable.

## Verification

```sh
pnpm test
pnpm typecheck
pnpm format:check
pnpm test:e2e
pnpm test:ai:live
pnpm test:translation:live
pnpm test:popup
```

Stop the normal dev server before browser suites, which reserve the same ports with temporary databases. AI live tests use the real pinned model with simulated sign-in and an unpacked extension. They select a sentence without clicking Translate and check matching explanations/examples in the saved library.

Sources: [Ollama structured outputs](https://docs.ollama.com/capabilities/structured-outputs), [official Qwen3 model](https://ollama.com/library/qwen3:4b).
