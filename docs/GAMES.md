# Classified game practice

Open **Game practice** from the library sidebar or `/games`, using the existing Google sign-in. Games are English-first and do not require a saved vocabulary item. The catalog has 179 immutable tasks: 83 original challenges and 96 tasks built from [reviewed open text](PRACTICE_SOURCES.md).

## Choose a mode, level and theme

Listening modes are **Dictation**, **Picture choice** and **Listening questions**. Speaking modes are **Speaking questions**, **Riddles**, **Puzzles** and **AI PK**. Open-ended source questions belong to Speaking questions, rather than the puzzle category. Every mode has content at all ten levels. Theme and content-source selectors show available reviewed material; not every theme or source appears at every level. The source selector stays visible when a selected source has no questions at that level, with an explicit empty state.

Levels 1–3 begin with words, phrases and short everyday responses; Levels 4–7 add connected sentences, reasons and short narratives; Levels 8–10 use longer passages and more complex tasks. These are editorial game difficulty labels, not CEFR levels or validated mastery evidence. Content is not cloned simply to fill a category.

## Scored level runs

**Start level** creates a server-owned run of up to three different matching questions in a fixed order. A category with fewer matching questions uses the actual count, which the setup screen displays. Listen and submit each answer. Speaking recordings are assessed automatically after stopping. Between questions, inspect feedback and choose **Next question**.

After the final confirmed answer, the level result appears automatically. Its score is the rounded mean of the saved question scores. A mean of **70 or above** passes and unlocks the next level **in that mode only**. The result offers two actions:

- **Retry** starts a fresh run at the same level and filters, preserving the previous result.
- **Next** starts the following level after a pass. It preserves the theme/source where matching content exists; otherwise it falls back to All themes, then All sources. It is disabled after a failed/unscorable run and at Level 10.

The result includes expandable per-question corrections, speaking evidence, IPA guidance and source credits. Any unscorable recording leaves the entire level score unavailable: it is not converted to zero or a false pass. AI quota/provider failures retain the same pending answer event/recording for an explicit **Retry answer**; that network retry differs from Retry for a completed level.

The run ID is in the page URL. Refresh restores server-confirmed questions, results and progress. Unsent recordings remain local and require recording again after a reload; the application never persists the raw microphone recording. Use **Refresh level** if a saved result has not yet appeared. Changing pages/account or expiring the session stops microphone tracks and local playback.

Legacy unscoped game results remain readable and retain previously earned access. New runs unlock modes independently. Repeated practice does not erase a pass. Adaptive recommendations use recent distinct, independent evidence and stay within a mode's earned access. No game outcome changes FSRS or vocabulary-recall progress.

## Tasks and feedback

Dictation compares words while ignoring case and punctuation; replaced, missing and extra spans are highlighted red. Picture/comprehension questions are graded against server-owned choices. Speaking uses rubric v2 with quoted evidence, audio-quality guards, reference IPA guidance and a relevance ceiling. The game-question mark averages the speaking overall and task-response estimates. Short beginner responses are assessed against their stated criteria; an advanced explanation is not required.

AI PK uses Echo, a clearly labelled simulated training opponent, not a real player. Fixed targets range from 43 at Level 1 to 85 at Level 10. A match outcome is shown alongside the level score; progression is based on the 70-point level threshold rather than claiming victory as proficiency.

Imported text keeps source, author and license links. Playback is generated AI speech, not the publisher's original recording. The public catalog includes audio text for playback: this is a learning activity, not an anti-cheat exam. Successful playback is client-reported.

## Persistence and API

Run `pnpm db:generate` and `pnpm db:migrate` after deployment. The local `pnpm dev` launcher deploys migrations automatically. The additive migration introduces `GameLevelSession`; existing `GameAttempt` rows gain an optional session reference. No historical answers are rewritten.

- `GET /v1/games`: public tasks, legacy skill access, per-mode access and recommendations.
- `POST /v1/games/levels`: validated kind/level/theme/source and event ID create an owned ordered run; identical start retries return the same run, changed settings cannot reuse its event.
- `GET /v1/games/levels/:id`: owned run, saved receipts, completion score, pass status and allowed next level.
- `POST /v1/games/attempts`: existing bounded answer schema plus optional `levelSessionId`. Scoped answers must match the next unanswered run task. Matching event retries coalesce/replay; conflicting or out-of-order answers are rejected. Unscoped requests remain for legacy compatibility and do not bypass their old access checks.

The server chooses questions, enforces ownership/order/access and computes completion under the user's transaction lock. A unique session/round constraint prevents two saved answers for one slot. A browser cannot submit a level score or completion flag. Completed records cascade on account deletion; pending assessments recheck ownership before saving. Stored feedback contains a transcript and score, never raw/base64 audio or Blob URLs.

Speaking retains the decoded 2 MB/60-second limit and 3 MB route JSON limit, recording signature checks, two assessment pipelines, six new spoken game requests per user/minute and twelve per process/minute. Local English audio uses pinned Phonon and the existing persisted cache; recorded-answer assessment uses the configured Gemini key and project quota without paid fallback. Uploaded audio and exercise context are processed under the existing Google setup; unpaid-service data may be used for product improvement.

## Verification

Regression tests use isolated PostgreSQL databases, simulated sign-in, deterministic AI/audio fixtures and synthetic browser recordings. They cover all modes/levels, themes and source selection, owned/idempotent starts, ordering, concurrent answers, means and unscorable runs, category-specific unlocks, legacy access, top-level completion, deletion, automatic speaking, microphone cleanup, source attribution, Retry/Next and URL recovery. No personal microphone upload or new credential is needed.
