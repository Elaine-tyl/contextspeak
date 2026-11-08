# Local AI translation implementation plan

**Goal:** Automatic six-language learning cards with one saved result.
**Architecture:** Add a bounded Ollama adapter behind the existing translation interface; persist versioned learning JSON and render it in the selection card and library. Keep explicit LibreTranslate/fixture alternatives.
**Spec:** docs/superpowers/specs/2026-11-07-ai-translation-design.md
**Constraints:** English UI; explanations use target language; local model; no invented IPA; automatic by default, no initial Translate click; collection consent unchanged. Preserve previous popup redesign.

- [x] Add failing adapter regressions for exact selected scope, six-language catalog, schema/size/error/timeout handling, and bounded inference.
- [x] Add learning contracts and additive migration, then persist/share the same structured result across transient and collected lookups.
- [x] Implement schema-constrained local AI adapter using the official Qwen3 4B model and recorded digest.
- [x] Render automatic dark learning cards and library details. Preserve existing explicit automatic-off preference; defaults are on for new users, and enable the user's requested existing preference through its settings.
- [x] Run unit/API/browser and real-model samples across six languages; verify automatic selection-to-library flow. Restore normal app and prepare the currently loaded extension folder for reload.
