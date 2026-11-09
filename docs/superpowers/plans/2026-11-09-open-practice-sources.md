# Reviewed open practice sources

Continue the user's approved implementation workflow: collect free sources online and make selected content usable in practice, with concise reproducible code.

- Verify publisher reuse terms and each imported item's metadata. Prefer explicitly CC0 sentences and original VOA educational transcripts; exclude third-party media and news-agency material. Research openly licensed stories for longer practice.
- Store a small reviewed, versioned text corpus with source URL, author/contributor, license link, retrieval date and SHA-256. Keep it separate from personal saved vocabulary. No startup scraping, new dependencies, paid service or RAG.
- Build deterministic listening dictation and original speaking-response tasks from reviewed text, aligned to existing ten levels. Keep source attribution visible in tiles and rounds, with an optional source filter.
- Add offline validation and an explicit online source verification command. Reject unapproved licenses, duplicate text/IDs, missing attribution, changed source text and out-of-range difficulty; do not silently rewrite historical exercises.
- Add regression tests for validation, provenance, catalog integration, source filtering and actual save/scoring behavior. Run unit/API/browser checks, typecheck, format/build checks and commit locally.

## Result and verification

Collected 48 excerpts (24 Tatoeba CC0 sentences, 12 VOA lesson excerpts and 12 CC BY 3.0 story excerpts) across 30 documents. Added 96 practice tasks, bringing the catalog to 179, with all ten levels represented. Source data is shared/versioned and remains separate from personal vocabulary.

- 198 unit/API tests across 40 files passed, including source-license validation, text/ID deduplication, SHA integrity, pinned multi-page Markdown extraction, network retry behavior, catalog provenance and durable imported listening/speaking receipts.
- 50 browser tests passed. The source filtering regression first exposed ambiguous implicit label text; an explicit label/select association fixed it. Browser checks cover filtering, credit, hidden pre-answer text and saved scoring through the local API.
- All 30 original source documents passed online content/metadata verification; the default offline corpus command passed. Typecheck, formatting and diff checks passed. The extension build passed during the browser run.
- Production web build passed. No new dependencies, database migration or RAG. AI playback is generated on demand and cached using existing infrastructure.
