> Browser update (2026-11-12): four tracks and twenty-one native React/Three.js modes. Vocabulary and Reading each have four ten-level games, with 240 original text tasks altogether. Text games require no speech playback or AI quota. Existing server grading, owned level sessions, retry safety and mode-specific unlocks remain; no database migration is needed. Quiz marks stay separate from FSRS and audio skill summaries. Game levels are editorial practice difficulty, not certified IELTS bands.

Text puzzle update: **Word scramble** reconstructs words from definition clues; **Mini crossword** uses two intersecting clue words and marks crossing conflicts; **Evidence hunt** selects a supporting sentence directly in a short passage; **Sentence puzzle** builds a reference summary from word tiles. All have ten levels and three tasks per level. Their extra 120 tasks use separate puzzle vocabulary and new passages; no previous quiz word prompt or ordering passage discloses the solution. Summary marks check the supplied reference sentence, rather than claiming that other paraphrases are invalid English. Crossword feedback marks each clue separately, with correction after submission. The eight text modes now contain 240 original tasks in total.

# Classified game practice

Open **Games** from the sidebar or `/games`, using the existing Google sign-in. Games are English-first and do not require a saved vocabulary item. The catalog includes original challenges and tasks built from [reviewed open text](PRACTICE_SOURCES.md), plus 240 original vocabulary and reading exercises.

## Choose a mode, level and theme

Listening modes are **Scene words**, **Dictation**, **Missing words**, **Picture choice**, **Listening questions**, **Spelling bee**, **Sentence builder**, **Sound detective** and **Quick fire**. Speaking modes are **Speaking questions**, **Riddles**, **Puzzles** and **AI PK**. Vocabulary modes are **Word meanings**, **Word in context**, **Word scramble** and **Mini crossword**. Reading modes are **Reading questions**, **Paragraph order**, **Evidence hunt** and **Sentence puzzle**. Open-ended source questions belong to Speaking questions, rather than the puzzle category. Every mode has content at all ten levels. Theme and content-source selectors show available reviewed material; not every theme or source appears at every level. The source selector stays visible when a selected source has no questions at that level, with an explicit empty state.

The existing twenty modes use the same editorial bands: **Intermediate at 1–3**, **Upper-intermediate at 4–7**, **Advanced at 8–10**. These are practice labels, not certified CEFR levels, IELTS bands or validated mastery evidence.

The seven modes other than Missing words have 90 new original v3 tasks: three dictations and one each of picture choice, comprehension, speaking response, riddle, puzzle and AI PK per level. Dictation starts with connected sentences and builds to longer qualified passages. Picture choice uses paraphrase and inference with the existing four images. Comprehension tests reasons, constraints and conditional conclusions. Riddles have distinct plausible answers; practical puzzles state workable constraints. Speaking asks for reasons at 1–3, alternatives or trade-offs at 4–7 and a qualified assumption or limitation at 8–10. Riddles instead ask for supporting clues, adding an alternative interpretation at advanced levels. Relevant alternative answers are accepted.

Original news-style scenarios are fictional practice, clearly labelled separately from credited real reports. Foundational original/Tatoeba/story/introductory-lesson tasks remain archived for existing runs and scores; current source selection offers the reviewed news reports.

## Scored level runs

**Start level** creates a server-owned run of up to three different matching questions in a fixed order. A category with fewer matching questions uses the actual count, which the setup screen displays. Audio games require completed playback before answering; vocabulary and reading games can be answered immediately. Speaking recordings are assessed automatically after stopping. Between questions, inspect feedback and choose **Next question**.

After the final confirmed answer, the level result appears automatically. Its score is the rounded mean of the saved question scores. A mean of **70 or above** passes and unlocks the next level **in that mode only**. The result offers two actions:

- **Retry** starts a fresh current-edition run at the same level, preserving the previous result. It keeps the theme/source where matching current tasks exist, otherwise falls back to All themes and All sources.
- **Next** starts the following level after a pass. It preserves the theme/source where matching content exists; otherwise it falls back to All themes, then All sources. It is disabled after a failed/unscorable run and at Level 10.

The result includes expandable per-question corrections, speaking evidence, IPA guidance and source credits. Any unscorable recording leaves the entire level score unavailable: it is not converted to zero or a false pass. AI quota/provider failures retain the same pending answer event/recording for an explicit **Retry answer**; that network retry differs from Retry for a completed level.

The run ID is in the page URL. Refresh restores server-confirmed questions, results and progress. Unsent recordings remain local and require recording again after a reload; the application never persists the raw microphone recording. Use **Refresh level** if a saved result has not yet appeared. Changing pages/account or expiring the session stops microphone tracks and local playback.

Legacy unscoped game results remain readable and retain previously earned access. New runs unlock modes independently. Repeated practice does not erase a pass. Adaptive recommendations use recent distinct, independent evidence and stay within a mode's earned access. No game outcome changes FSRS or vocabulary-recall progress.

## Tasks and feedback

**Missing words:** open `/games?mode=gap-fill`, start a level and play its AI
sentence. Fill the inline blanks, then press Enter or **Check answer**. All
blanks require an answer after completed playback. Marks count only missing
words; case and punctuation are ignored. Extra words in a blank count as an
incorrect answer and receive a correction. Feedback shows the full sentence,
each correct word and red corrections, including in saved level reviews.
Forty current original sentences cover Café, Making plans, Travel and Academic study,
with longer connected sentences, more complex clauses and academic vocabulary.
The v3 edition raises the target words themselves: Levels 1–3 use intermediate
vocabulary, 4–7 upper-intermediate and 8–10 advanced. Examples include allergens,
reimbursement, persuasiveness, confounding and generalizability. These are editorial
practice bands rather than certified CEFR levels. Answer fields grow from entered
text only, so long words stay readable without revealing expected answer length.
Levels 1–2 have two blanks, 3–4 have three, 5–6 have four, 7–8 have five and
9–10 have six. Eighteen credited VOA news excerpts cover News · education, technology, work
and environment, with source, author and original publication date. These are archive
features, not a live news feed. Academic study is independently written;
levels are not IELTS bands. Free official IELTS/British Council exercises are
linked under **Free news and IELTS practice**, alongside BBC Learning English
news vocabulary and course resources. BBC text/audio is not imported.

Seventy v1/v2 originals remain archived solely for existing runs and saved feedback.
An earlier-edition notice explains that Retry starts the current edition;
earned levels are preserved. Missing words starts at Level 1 even for users
with legacy listening progress. It reuses cached AI speech and requires no new
provider, database migration or AI grading.

Dictation compares words while ignoring case and punctuation; replaced, missing and extra spans are highlighted red. Picture/comprehension questions are graded against server-owned choices. Speaking uses rubric v2 with quoted evidence, audio-quality guards, reference IPA guidance and a relevance ceiling. The game-question mark averages the speaking overall and task-response estimates. Short beginner responses are assessed against their stated criteria; an advanced explanation is not required.

AI PK uses Echo, a clearly labelled simulated training opponent, not a real player. Current fixed targets range from 58 at Level 1 to 85 at Level 10; archived matches retain their original targets. A match outcome is shown alongside the level score; progression is based on the 70-point level threshold rather than claiming victory as proficiency.

Imported text keeps source, author and license links. Playback is generated AI speech, not the publisher's original recording. The public catalog includes audio text for playback: this is a learning activity, not an anti-cheat exam. Successful playback is client-reported.

## Persistence and API

Run `pnpm db:generate` and `pnpm db:migrate` after deployment. The local `pnpm dev` launcher deploys migrations automatically. The additive migration introduces `GameLevelSession`; existing `GameAttempt` rows gain an optional session reference. No historical answers are rewritten.

- `GET /v1/games`: public tasks, legacy skill access, per-mode access and recommendations.
- `POST /v1/games/levels`: validated kind/level/theme/source and event ID create an owned ordered run; identical start retries return the same run, changed settings cannot reuse its event.
- `GET /v1/games/levels/:id`: owned run, saved receipts, completion score, pass status and allowed next level.
- `POST /v1/games/attempts`: existing bounded answer schema plus optional `levelSessionId`. Vocabulary, reading and Missing words require a level session. Missing words uses ordered `gapAnswers` (at most six answers of 80 characters each). Scoped answers must match the next unanswered run task. Matching event retries coalesce/replay; conflicting or out-of-order answers are rejected. Unscoped requests remain for legacy modes only.

The server chooses questions, enforces ownership/order/access and computes completion under the user's transaction lock. A unique session/round constraint prevents two saved answers for one slot. A browser cannot submit a level score or completion flag. Completed records cascade on account deletion; pending assessments recheck ownership before saving. Stored feedback contains a transcript and score, never raw/base64 audio or Blob URLs.

Speaking retains the decoded 2 MB/60-second limit and 3 MB route JSON limit, recording signature checks, two assessment pipelines, six new spoken game requests per user/minute and twelve per process/minute. Local English audio uses pinned Phonon and the existing persisted cache; recorded-answer assessment uses the configured Gemini key and project quota without paid fallback. Uploaded audio and exercise context are processed under the existing Google setup; unpaid-service data may be used for product improvement.

## Verification

Regression tests use isolated PostgreSQL databases, simulated sign-in, deterministic AI/audio fixtures and synthetic browser recordings. They cover all modes/levels, themes and source selection, owned/idempotent starts, ordering, concurrent answers, means and unscorable runs, category-specific unlocks, legacy access, top-level completion, deletion, automatic speaking, microphone cleanup, source attribution, Retry/Next and URL recovery. No personal microphone upload or new credential is needed.

Missing-word regressions additionally cover equal partial marks, no credit for
visible words, per-blank corrections, malformed/conflicting submissions,
unscoped progression protection, lost-response replay, mobile layout and Enter.

Missing words verification (2026-11-10): 501 source tests, eight Games browser
cases, typecheck/format and production web build passed. Real local Phonon
playback completed in the signed-in website. See the [live exercise](playtest/missing-words/live-missing-words.png)
and [mobile corrections](playtest/missing-words/missing-words-feedback.png).


Harder Missing words verification (2026-11-11): **505 source/API tests across
68 files**, **nine Games browser cases**, typecheck, repository format check and
production web build passed. Tests cover v1 run/receipt preservation, current
Retry selection, six-blank partial credit, news attribution/filtering, official
resource links and narrow-screen layout. Six news excerpts and staff authorship
were verified against the live source HTML. Real local Phonon playback completed
in a fresh signed-in Level 4 Academic study run; no answer was submitted to that
learner account. See [live academic practice](playtest/harder-missing-words/live-academic.png)
and [six blanks on mobile](playtest/harder-missing-words/academic-six-blanks.png).

Completed older results now show their earlier-edition notice, and saved news
answer reviews repeat the publication date and archive context. Shared renderers
keep these labels consistent with live rounds.


Intermediate–advanced vocabulary verification (2026-11-11): **509 tests across
68 files**, **nine Games browser journeys**, typecheck, repository formatting and
production build passed. Twelve new excerpts and their original staff bylines
were checked against downloaded live report HTML. Tests cover archived v2
scoring/receipt replay, current v3 Retry, the three new news filters, BBC resource
links and long entered words in a six-blank mobile round. Real local Phonon
playback completed for a Level 4 News · work exercise; no answer was submitted to
the learner account. See [news exercise](playtest/news-vocabulary/live-work-news.png)
and [advanced mobile fields](playtest/news-vocabulary/academic-six-blanks.png).
The independent scoped review found no new issues. It verified source content
and targeted tests, but could not independently compare every v2 byte because
those templates were untracked before this upgrade; the extraction preserved
the array and literal replay regressions passed.


## Aligned edition verification — 2026-11-11

The seven-mode update adds 90 original v3 tasks and 18 news-speaking v2 tasks.
There are 184 active tasks (130 originals and 54 news tasks) and 267 archived
variants. A pre-change snapshot of all 215 historical non-gap tasks is checked by
SHA256 in `current-games.test.ts`; every historical field is preserved apart
from the intentional archive flag. Saved foundational-source runs remain owned,
scorable and replay safe. Their Retry uses available current themes/sources.

Verification: **515 tests across 69 files**, **ten Games browser journeys**,
typecheck, repository formatting and production build passed. Browser tests cover
automatic speaking, simulated PK, all mode guides, scoring, URL reload, red
corrections, current source filters and archived-source Retry. The shared edition
notice and source publication date now appear in completed results as well.

The scoped review found two content issues. Advanced pictures were revised to
require interpreting a changed plan or integrating several constraints, rather
than recognizing one isolated keyword. Original spoken scenarios are explicitly
labelled fictional; credited real news keeps its source attribution. Both fixes
have regression tests that failed before the changes and pass afterward. Bands
remain editorial and AI marks are coaching estimates, not certified exam scores.


Live local Phonon playback completed for an unlocked Level8 picture inference
round in the signed-in website, with no answer submitted to that learner account.
See the [advanced game preview](playtest/aligned-games/live-advanced-picture.png).

## Empty catalogs during local development

`pnpm dev`, `dev:local` and `dev:test` watch imported API code and question JSON.
After editing a backend dependency, the launcher restarts using the existing
persistent development database. An already-running older launcher needs one
manual restart to enable watching. Frontend hot reload alone cannot update the
API's in-memory question catalog.

If a whole mode is absent from the API response, its setup explains that the
server content is not loaded. Refresh progress re-fetches the catalog. If only a
source/theme filter has no matches, Clear filters restores the current level's
available questions. Selecting another level resets both filters.

## Reading questions: read, listen and recall

Current reading editions highlight 3–6 curated content words in a short passage.
Press **Start** once to hear cached AI narration while reading. Only a completed
play replaces the highlighted words with letter boxes. Fill all blanks and use
**Check answer** for per-word feedback and a saved score. **Replay audio** does
not reveal the original words or clear answers. Errors and interrupted audio
leave the preview available for retry. New reading practice needs working AI
speech or previously cached audio; grading the blanks uses no LLM request.

These editions use new v2 IDs. Existing v1 multiple-choice sessions keep their
original questions, answers and progress; start a new level to use recall.

Long recall passages are read as consecutive sentence-boundary chunks of at most
600 Unicode characters, using the same cached speech endpoint. The whole
passage counts as one completed play only after every chunk finishes. A failed
or interrupted chunk keeps the preview; retry reads the passage again. This
keeps existing per-request voice limits and authentication checks in place.

## Scene words

Choose **Listening games → Scene words** for a 3D room with a single-word
listening gap. The first release contains 30 original prompts across ten levels,
covering bedrooms, kitchens, cafés, markets, apartments, restaurants and airports.
Play the AI sentence, type the missing word and press Enter or Check answer.
The server saves the grade and then returns a word notebook with an English
meaning, a Chinese gloss, common phrases and a new example. Speaker icons reuse
the existing cached AI voice. No LLM call is needed for the editorial notebook.

Each level has three questions; a 70+ level score unlocks the next scene level.
Other listening-game results do not skip this new mode's first level. Existing
scores and vocabulary review schedules remain unchanged.

Rooms are original procedural Three.js furniture with rounded geometry, soft
shadows and subtle pointer/light reactions. Animation follows the 3D preference
and pauses when hidden or outside the viewport. WebGL failure uses a CSS room;
native keyboard inputs, grading and the word notebook remain available. Sound
and success confetti use the shared game preferences. No new art downloads,
paid services or game engine are required.

Scene-word notebooks are also available under **Review answers** on the completed level, including after a reload.
