# Game catalog repair and free reading content

- Reproduce the signed-in empty reading catalog against the live server. Confirmed: the frontend updated while the API retained its older module graph.
- Add regression coverage for development reloads, stale-catalog recovery and active content coverage.
- Watch backend dependencies during development; preserve persistent PostgreSQL data and existing sessions.
- Distinguish missing server content from an empty source/theme filter.
- Reuse reviewed, credited VOA excerpts for original reading questions. Keep original questions available without external downloads or AI quota.
- Verify tests, types, formatting and the real signed-in game launch. Do not submit invented learner scores.
