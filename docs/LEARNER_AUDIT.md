# Learner flow audit — 2026-11-10

Scope: Dashboard, learning goals, vocabulary priorities and entry into practice. The existing signed-in Chrome account was inspected without changing its stored learning results. Save/reset/conflict/failure mutations were exercised with disposable learners. Screenshots from this run remain local in `/private/tmp/contextspeak-learner-audit/`; they are not bundled with the repository because they show personal learning data.

| Step | Learner task | Finding and resulting behavior |
| --- | --- | --- |
| 1 | Decide what to do today | Totals worked, but the first screen required too much reading/scrolling. A compact Review / Listen / Speak entry now provides actual next actions without inventing streaks or completion. Empty skill summaries say no eligible score and explain why older/unscorable history marks may be excluded. |
| 2 | Choose a goal | Saved and unsaved choices looked alike. Drafts now show an unsaved state; unchanged Save is disabled. Refresh keeps drafts, conflicting saved changes require reload, and optional expected goals reject stale writes. Reset and actual saving remain explicit. |
| 3 | Ask for suggestions | Total context count did not represent eligibility for another analysis. Readiness now counts different new contexts. Cached/unavailable/insufficient states are distinct; manual goals remain usable. Uncalibrated confidence percentages are replaced with inspectable supporting excerpts. |
| 4 | Pick a vocabulary item | Twenty entries and apparent /100 grades made the list hard to scan. Five compact rows are shown first, with translation-language labels, matched goals, expandable reasons and the full list on request. Missing-frequency normalization rewarded unknown data; fixed-scale evidence contributions retain nulls without an artificial boost. |
| 5 | Start Speaking | The Speaking shortcut led through the hub to Listening. The final live-account check found an entire saved error-message passage being forced into a word-use prompt. Quick-start now prefers an expression of at most six words/60 characters and displays it; longer lookups remain available explicitly. Dashboard links open the selected skill, prefer a matching confirmed scenario and provide a return to Progress. Scenario changes preserve Speaking; removing a Conversation scenario falls back safely. |
| 6 | Use a phone/keyboard or recover from failure | Responsive checks cover 375px reflow and 44px action buttons; focus indicators are visible. Failed saves retain drafts and priorities, avoid automatic resubmission and prompt checking refreshed data before retrying. These checks do not establish complete accessibility compliance. |

## How I would use it

Start with the due review queue. Choose one saved expression, listen without revealing the text, then answer a short question aloud using it. Read the feedback and retry deliberately. Use Progress to choose the next task and compare independent practice over time, rather than treating a lookup count, keyword match or AI mark as mastery. Set one useful conversation goal first; use AI suggestions as optional prompts to inspect.

## Pilot recommendations and limits

Keep the first pilot focused on whether learners can complete that loop independently. Observe where they hesitate, whether priorities feel relevant, and whether feedback changes their next answer. The current three everyday-goal categories do not cover every academic/work context; do not infer personal needs from one excerpt. Gather acceptance/dismissal feedback before expanding the taxonomy.

This audit combines direct interaction, screenshots, code review and automated regressions. It is not a study with recruited learners. No new private excerpts or microphone recording were submitted for the audit. Human speech-mark agreement across accents/noise, actual provider quota/cost measurements, full accessibility testing, deployment/geography/retention decisions and real backup/media recovery remain the gates in [PILOT.md](PILOT.md).

Verification: 261 unit/database regressions and 60 browser tests passed, including new priority, eligibility, stale-goal, failure recovery and speaking-route cases. Typecheck, formatting, website/extension builds and diff checks passed. The normal app was restarted after testing. Production release gates remain open.
