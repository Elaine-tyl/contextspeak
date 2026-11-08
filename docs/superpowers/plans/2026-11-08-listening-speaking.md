# Free listening and speaking implementation plan

Goal: implement the selected on-device practice approach in [the design](../specs/2026-11-08-listening-speaking-design.md). Guided self-checks stay separate from FSRS; no audio upload, cloud speech, automated scores or new runtime dependencies.

- [x] API test first: owned successful encounter produces a bounded task; another owner/failed lookup is rejected; strict submissions are idempotent and cannot affect FSRS; deletion cascades.
- [x] Add shared practice contracts, an additive attempt migration and a focused practice service/controller. Bind saved metadata and server-generated task snapshots to the owned lookup; serialize per-user event IDs.
- [x] Browser test first: hidden transcript → local playback → response/reveal → saved self-check, separate speaking record/replay; denied microphone and save failures recover; remote voices excluded; navigation stops tracks.
- [x] Add focused local speech/recording helpers and a dedicated practice page linked from successful vocabulary details. Clean up playback, microphone tracks, timers, object URLs and late permissions on session change/navigation. Bound recording to 60 seconds/10 MB.
- [x] Run unit/API and full browser regressions, type/format checks and production build; review, document limitations, apply local migration and commit locally. The seven prior commits were already pushed to main at 3e210e2.

Builder check: no blocking decisions remain after the local approach choice. Count listening only after successful playback, use detected source language, keep metadata bounded and clear private state when sessions expire/change. This is practice rather than validated listening/speaking mastery.

Validation: 113 unit/API tests and all 29 default browser tests passed. Typecheck, formatting, extension build and production web build passed. Nine browser cases cover practice, including native audio encoding/decoding from a generated stream and real authenticated API saves. Voice/physical-microphone quality still needs a normal-device smoke test.

Review fixes: speaking now requires replay completion; unconfirmed saves have an explicit new-attempt action; replay audio is paused and detached on cleanup. Failing regressions were observed before the fixes. A native-audio check also exposed React development remount cleanup clearing the recording source; setup now restores it and the source/decoder regressions pass. No review findings were deferred.
