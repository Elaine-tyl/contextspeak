# Multilingual AI translation and examples

## Intent

Extend ContextSpeak's working capture-to-library flow with a rich translation card inspired by the supplied reference. Keep the service local and free of paid API charges. Initial source and target languages: English, Chinese, Japanese, Korean, Spanish, French; source also supports automatic detection. UI labels remain English; explanations and translated examples use the selected target language.

## Recommended approach

Add one Ollama translation adapter using the official pinned qwen3:4b model. Generate translation and learning details together in one schema-constrained response and validate it with Zod. Persist that same response for the selection panel and library. Keep the existing LibreTranslate provider available as an explicit alternative; do not silently label its plain output as AI explanations.

Alternatives: a hosted AI API could reduce local resource use but changes the user's free/local preference. Fast machine translation followed by a separate AI enrichment call could show an early result, but adds two result lifecycles and potential inconsistencies. Start with one validated result and a clear loading state.

## Card behavior

- Selected word or phrase: contextual meaning, part of speech when applicable, up to two common bilingual phrases, and two bilingual usage examples.
- Selected sentence: translation of exactly that sentence, a short explanation of a useful phrase or construction, and a bilingual usage example. Do not force a word-level part of speech onto a sentence.
- Source auto-detection and six-language target picker, with responsive dark styling consistent with the approved popup reference.
- Preserve original selected text. Context can disambiguate meaning but must never replace the selection as the text being translated.
- Render all generated strings as text, label examples as AI-generated, and omit unavailable fields. Do not invent phonetic transcription or show unsupported pronunciation/audio controls.

## Data and reliability

Extend the existing stored translation with versioned structured learning details; old records remain readable. Preserve immediate durable lookup saving, account ownership, explicit collection consent, exclusions, and shared results. Send only selected text and a bounded sanitized excerpt to the local model, never the page URL or full page. Show useful timeout/unavailable errors while retaining saved lookups. Adjust result waiting to accommodate local generation latency; limit output sizes and concurrent inference.

Record the model digest and prompt/schema version for reproducibility. Treat supplied webpage text as data rather than model instructions. JSON schema enforcement ensures response shape, not semantic correctness; require language-pair and selected-scope checks plus human review of live samples before expanding the language list.

## Verification

Regression tests cover exact selected text versus context, word/sentence response shapes, malformed or oversized responses, prompt-injection-like input, model errors/timeouts, language persistence, legacy records, ownership and one shared stored result. Live checks cover all six languages with bilingual examples; browser verification covers automatic English-to-Chinese translation and matching saved learning details.

## Status

User approved the six-language local AI implementation and requested automatic selection translation with no initial button. Live checks exposed language errors in the older installed Qwen2.5 model, so implementation moved to official Qwen3 4B. AI cards explicitly accept up to 600 characters to keep structured generation within a bounded local output budget. The prior popup work is preserved.
