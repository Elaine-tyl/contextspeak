# Café: voiced ordering and a conversation with a friend

User request: NPCs ask their questions aloud. Arrive and order at the counter,
then sit with a friend who starts a conversation. Research better free 3D people
and environments, including GitHub sources.

Implementation choice: extend the current Godot/local-AI game, retaining free
local Phonon, Whisper and Qwen. A deterministic journey owns ordering, walking,
seating and three conversation turns. Qwen supplies bounded acknowledgement and
feedback for preferences, weekend plans and an invitation; it does not control
movement, rewards or order success. This guided chat is the first version of the
requested friend conversation, not an unrestricted AI agent.

Browser speech is decoded and played by a dedicated Web Audio owner; the initial
Talk gesture unlocks audio. Native speech stays in Godot. Start, finish, failure,
cancellation and replay are visible and testable. Do not equate WAV generation
with audible playback. Cold local generation may take time; retain replay and
manual continuation if voice fails.

After the final spoken barista line, automatically walk to the table and sit.
A visible Join friend action also allows continuing early or after a voice error.
The friend starts speaking on arrival, acknowledges each meaningful reply and asks
the next question. Record/stop still checks automatically; typed replies remain
available. Failed recognition/chat checks do not advance or discard the answer.

Persist order and friend progress locally in a versioned envelope, migrate old
order-only saves, and reject chat progress without a completed order. Reset and
close must cancel late voice, recording and model callbacks. Task completion and
conversation progress remain separate from pronunciation/proficiency scoring.

Free-asset research must distinguish the exact free edition from paid extras,
record author/license/format and browser/animation fit, and keep provenance for
any imported bytes. Avoid replacing working characters with an unrigged model or
shipping an asset without its license evidence.
