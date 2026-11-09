# Learning progress dashboard

Open `/progress` or **Learning progress** in the library; **Progress** is also in the shared header. Sign-in returns to this allowlisted route. All metrics are scoped to the authenticated account.

Review totals are lifetime saved vocabulary, active/paused cards, currently due cards and completed review events. Recent rating counts and up to five words repeatedly rated Again/Hard cover the last 30 days. These ratings are learner reports, not mastery labels.

Listening/speaking averages use at most 100 scored vocabulary attempts and 100 scored game attempts per skill in the last 30 days. Filtering precedes sampling: self-check-only records, old speaking rubrics and unreliable audio are excluded. Unscorable v2 speech counts are reported separately. A missing score is shown as unavailable, never as zero. Mixed practice marks are not comparable exam scores or CEFR levels.

Speaking focus needs at least two different independent tasks with reliable scores; listening focus needs three. Repeating one task or receiving hints cannot alone establish a weakness. Game access comes from the same authoritative per-mode progression service as the game hub. Links open the correct mode; review links open the existing queue.

The read-only `GET /v1/progress` endpoint uses bounded owned SQL reads and aggregates, without a model call or schema migration. The page refreshes data every 30 seconds, checks the session every three seconds, refreshes on return, keeps loaded metrics if a refresh fails, and clears account data on expiry/account change. Automatic progress retries pause after errors until Refresh/return; session checks continue.

Regression coverage includes ownership, empty states, due versus paused cards, recent versus old events, unreliable-audio floods, self-check/old-rubric exclusion, repeated/assisted weakness evidence, per-mode levels, login redirects, stale-data display, initial failures and account cleanup. Real learner calibration remains a separate release gate.
