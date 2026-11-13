# More text puzzle games

Add Word scramble, Mini crossword, Evidence hunt and Sentence puzzle to the existing Vocabulary/Reading tracks. Keep current modes and saved IDs unchanged. Each new mode has ten levels and three questions per level. Use native controls, original content, existing owned level runs and server grading; no audio prerequisite or provider quota.

- Word scramble builds a word from shuffled letters using a definition clue.
- Mini crossword has two intersecting words with numbered clues, live letter grid, conflict indication and native letter inputs. Public metadata contains only positions, lengths and clues, not solution letters.
- Evidence hunt asks the learner to select a supporting sentence directly in a short passage. A selected sentence is an answer, not an automatically revealed solution.
- Sentence puzzle reads a short context then reconstructs a separate reference summary from word tiles. Use a new text bank so no existing ordering solution is published elsewhere.
- Reuse vocabulary clues for recall puzzles; use 30 new passages for reading puzzles. Grades remain practice outcomes rather than mastery/FSRS evidence.
- Add database-backed grading/content regressions, browser keyboard/touch/undo/no-audio flows, inspect screenshots and run the existing verification suite. Review for cross-catalog solution leaks.

## Completed

Added four modes, each with ten levels and three tasks per level. Crossword metadata carries two consistent intersecting entries; user guesses drive the grid and conflicts are visibly labelled. Evidence is selected directly in the original short passage. Tiles support undo and require completion before checking. Existing audio modes and all historical questions stay intact.

Review identified that sharing the existing word-meaning bank would disclose puzzle targets through public prompts. Replaced it with an independent 30-word bank, retaining the original vocabulary content unchanged, and added a catalog regression. New reading texts and separate summary wording prevent cross-mode ordering disclosures. Reviewer verified all 30 crossword layouts and the absence of direct prompt leaks.

Verification: 596 source tests across 79 files passed; 24 full browser flows passed. An existing letter-layout assertion now waits for finite input animations before comparing geometry, rather than measuring an animated letter mid-transition. After mobile visual QA, the motion toggle was moved below active game content to avoid covering answer controls; all eight literacy browser flows passed again, including non-overlap geometry. Typecheck, format check, production web build and diff whitespace checks passed. Actual screenshots in docs/images/contextspeak-{word-scramble,sentence-puzzle,crossword-mobile,evidence-hunt}.png use isolated fixture content.
