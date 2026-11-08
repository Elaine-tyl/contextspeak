# Vocabulary reviews

Open **Vocabulary candidates**, choose **Start reviewing**, then use **Review queue**. Recall the meaning before choosing **Show answer**, and rate honestly: Again (forgot), Hard (difficult), Good (recalled), or Easy (effortless). Only due cards appear. When the queue is empty, it shows the next due time and checks automatically when that time arrives or you return to the tab. Active recall cards are never automatically replaced. A failed load stops automatic checks; use **Refresh queue** to retry. Pause reviews from a candidate or a due card. Starting again resumes the existing schedule.

Enrollment is explicit and requires a successful saved translation. From a detail page, enrollment uses that selected encounter; candidate enrollment uses the latest successful translation. The prompt, answer and context are frozen from that lookup, so later translations do not silently change a review. Reviews do not consume Gemini quota. Saving ratings requires a connection; if a request times out, retry the same rating or refresh the queue. An unconfirmed request keeps its event ID so retries cannot count twice.

The backend uses pinned MIT-licensed [ts-fsrs 6.0.0](https://github.com/open-spaced-repetition/ts-fsrs), FSRS-7, with target retention 0.9 and fuzz disabled. This target is a scheduling parameter, not a guarantee. Dates are stored in UTC and displayed in the browser’s timezone. Schedules and rating logs remain server-owned; grades require an owned active due card and the current version. User-scoped event IDs and transactions prevent duplicate or competing grades.

This milestone tracks **self-rated vocabulary recall**, not listening/speaking skill or independently demonstrated mastery. It does not automatically enroll words, train personal FSRS parameters, send reminders or provide offline grading.

New database tables are additive: ReviewCard stores a frozen answer and schedule; ReviewEvent stores the rating log. Both cascade when their parent vocabulary item is deleted. `pnpm dev` deploys the migration; deployed environments should run `pnpm db:migrate` before serving the new API.
