# Listening and speaking practice — proposed first slice

Status: local approach selected by the user on 2026-11-08. This extends the approved capture, vocabulary and recall work.

## Goal

Use saved vocabulary for short listening and speaking sessions. Keep listening, speaking and FSRS vocabulary recall separate. This first slice is guided practice, not a validated language assessment.

## Approaches

1. **Recommended: free on-device practice.** Use browser speech synthesis with local voices only, plus microphone recording/replay. No new speech account, API bill or audio upload. Available voices and quality vary by device; clearly explain unavailable languages. Self-checks cannot provide pronunciation or comprehension grades.
2. **Cloud speech and AI feedback.** Add provider-backed synthesis, transcription and feedback. This needs a provider choice, measured quality, quota/cost limits and agreement on recording transmission and retention. It is a separate follow-up rather than a silent fallback.

## Learner flow

- Open a dedicated practice page from a vocabulary detail page, preserving the selected encounter. Only a successful saved translation is eligible.
- **Listening:** play the saved expression (or its bounded source-language example, when available), write what it means before revealing the transcript/meaning, then self-check. Track replay count and whether the answer was revealed. Count completed listening only after successful playback; use the detected source language and explain when no suitable local voice is available. Do not display the source expression as the page title before reveal.
- **Speaking:** record a short response or a sentence using the expression, stop and replay it, compare with the saved example, and self-check. Showing an example marks the attempt as assisted. This is guided production; no independent speaking mastery is inferred.
- Save listening and speaking attempts separately. Show recent practice with assistance indicators. Never automatically change FSRS ratings or mastery estimates.

## Data and boundaries

- Reuse the authenticated, owner-scoped vocabulary detail API for source material. Bind each saved attempt to the owned successful lookup and an idempotent event ID; snapshot its practice text/version server-side.
- Store bounded attempt metadata and optional typed listening response, not recordings. Preserve event IDs on retries and reject reuse with different payloads. Deleting the learning item cascades to its attempts.
- Source material and results remain private to the signed-in learner. A changed or expired session clears practice data and stops capture/playback.
- Record only after a deliberate click and microphone permission. Stop at 60 seconds or a 10 MB local buffer, and stop tracks on completion, navigation, cancellation, errors or account changes. Release object URLs and discard late permission results after navigation. Audio remains in browser memory and is lost on leaving the page.
- Use only a matching `localService` speech voice. Wait for voice availability changes; never silently send text to a remote voice. Handle missing voices, unsupported recording, denied permission, interrupted playback and empty recordings with recoverable messages.
- Microphone capture requires HTTPS or a loopback development origin. No background recording or browser speech-recognition service.

## Architecture

Add one focused practice controller/service, an additive attempt table, shared validated contracts and a dedicated client practice page. Keep playback and recording lifecycle code in small browser helpers. Reuse existing auth/CSRF checks, request error handling and styles. No new runtime dependency or change to translation providers.

## Acceptance checks

- Owned saved vocabulary opens practice; another account cannot read or submit it.
- Listening starts with transcript hidden; playback, replay and reveal are tracked accurately; unavailable or failed speech is never counted as completed listening.
- Microphone denial is recoverable; stopped/cancelled/navigated recordings release every track; late permission grants cannot keep a microphone running.
- Retried saves count once; altered duplicate events are rejected; failed saves retain the learner's attempt.
- Deterministic browser fixtures cover audio API failures and the whole flow; API integration tests verify ownership, input bounds, idempotency and no FSRS mutation.
- Existing capture, translation, library and review regressions remain green. No live provider calls are needed for tests.

## Later work

Novel scenario generation, held-out listening tasks, cloud transcription, intelligibility feedback, calibrated assessments and adaptive skill estimates follow this foundation. Do not advertise them as implemented.
