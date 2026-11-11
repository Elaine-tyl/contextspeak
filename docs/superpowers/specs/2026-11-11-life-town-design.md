# ContextSpeak Life: first playable day

Status: first milestone authorized on 2026-11-11. The user selected home,
walkable town, produce market and the existing cafe, delegating remaining design
choices. This is the first slice of the life simulation, not the completed world.

## Intent and scope

Learn spoken English by living a small, coherent day. A family member asks for
groceries, the player walks to the market, buys them, returns home, then orders a
drink and catches up with a friend. Successful conversation changes inventory,
money and the current task. Models cannot invent game actions or rewards.

Keep the earlier engine decision: Unity 6000.6.8f1/URP for 3D, Godot for the
separate 2D learning games. Browser and macOS share the same C# mission rules.
Existing cafe and website progress remain intact. Single-player local saves,
local Whisper/Qwen/Phonon and existing bounded audio caching remain the defaults.

## Approach

Extend the Unity project with a separate LifeTown scene, reached from the cafe
and a `?world=life` browser entry. Preserve ForestCafe as its own playable scene.
Use a small walkable neighborhood with open-front/cutaway interiors rather than
a gigantic continuous world. The town is small enough to load together; larger
districts/country maps will load separately later.

A scene-selector-only prototype would not validate walking and linked errands.
Building every requested region now would leave the central speech/correction
and transaction loop unproven. The user chose the linked first-day milestone.

## First-day content

| Place | NPC | Interaction | Result |
| --- | --- | --- | --- |
| Family home | Mom | Accept a dinner-shopping errand | Shopping list: three apples, two carrots |
| Produce market | Sam | Ask for apples, carrots, then choose cash/card | Cart, $7 purchase, groceries in bag |
| Family home | Mom | Hand over the groceries | Groceries removed; family dinner prepared |
| Forest cafe | Maya | Choose a drink, temperature, here/to-go, confirm | Menu, receipt, paid drink in bag |
| Cafe seating | Alex | Preference/reason, weekend plan, invitation/time | Social task and first day complete |

Start with $20. Apples cost $1 each; carrots cost $2 each. Cafe prices remain
coffee $4, latte $6.50, tea $3, lemonade $3.50. Monetary arithmetic uses integer
cents. Menu browsing cannot buy anything. Commit payment only after the required
accepted spoken step; refresh/replayed results cannot charge twice.

The home has a family identity and a visible dinner table. This slice does not
pretend to deliver configurable family relationships or autonomous daily schedules.

## Spoken learning contract

1. Approach an NPC and press E or the visible Talk control. The NPC speaks aloud.
2. Record an answer; stopping automatically transcribes and checks it locally.
3. Check meaning, required details and language. Natural alternatives and valid
   contractions are accepted. Punctuation/capitalization are not speech errors.
4. If a real wording/grammar error exists, show the complete corrected expression
   and one brief explanation. Remain on the same step and offer Hear correction.
5. A new spoken answer must meet the current goal and resolve the correction.
   It need not match the example word for word. Only then advance.

Typing is explicitly labelled rehearsal. It can receive feedback but cannot earn
a spoken mission step. Test-stage jumps are labelled practice and never overwrite
the earned save. An uncertain/unintelligible recording, unavailable model, rejected
answer or cancelled request preserves the current task and money.

Text transcription supports wording and communication feedback, not validated
pronunciation or acoustic proficiency scores. Do not fabricate pronunciation marks
from text. Retain a clear path to later audio-based pronunciation assessment.

## Shared implementation boundaries

- One authored JSON catalog supplies NPCs, item prices, prompts, goals and examples
  to Unity and the local companion. Validate its identifiers and bounds.
- A pure C# LifeMission owns step order, corrections and derived wallet/bag state.
  Its versioned save validates all prerequisites before restoring.
- The loopback `/life/review` endpoint uses the existing guest session/origin,
  body-size, concurrency and cancellation protections. The local model returns
  language feedback plus a bounded drink choice, never actions or balances.
- Deterministic goal checks reject missing quantities, negation and ambiguous
  purchase confirmations even if a small model marks them accepted.
- LifeGame connects proximity, microphone, AI voice and the mission. Epochs reject
  stale callbacks after closing, changing stage or unloading the scene.
- Browser semantic HTML and native runtime UI mirror the same C# snapshot. Menus
  appear only for relevant NPCs; shopping list, bag, wallet and destination remain
  visible without obscuring the world. User/model text is rendered as plain text.

## Art and interaction

Use verified existing CC0 models and original generated architecture. Cream plaster,
warm timber, muted sage, terracotta roofs/awnings, a planted square and clearly
readable shopfronts unify the scene. Preserve human-scale passages and colliders;
do not fill paths with decoration. Soft shadows, restrained postprocessing and
reused materials keep the scene practical for WebGL. Smooth third-person movement,
manual exploration and a route-to-objective helper serve both play and testing.

## Expansion order

1. This complete first day: home, market, cafe, correction loop and persistence.
2. School/class timetable and classroom answers; farm tools/growth/harvest; clothing
   shop with sizes/try-on; restaurant menus/ordering and seating.
3. Fishing, amusement park tickets/rides, apartments and neighborhood tasks.
4. Court/service-desk language simulations, airport/hotel and country districts.

US/Canada conversational English remains the initial regional direction. Later
country districts need authored local contexts, not cosmetic flag swaps. Legal
service tasks are fictional language scenarios rather than real legal procedure.
Full relationship simulation, decorating, seasons, multiplayer, leaderboards,
online synchronization and RAG are outside this first milestone.

## Acceptance and evidence

- Walk between the home, market and cafe and trigger only nearby NPCs.
- Invalid/repaired answers cannot advance; a fresh valid spoken retry can.
- Typed rehearsal, cancelled/late responses and test jumps earn no real steps.
- Verify the complete $20 → $13 → cafe payment loop and grocery handoff.
- Restore the earned step, correction, choices, bag and money across reloads.
- Exercise real WebGL input/recording bridge with fixture STT/model/audio, plus
  live local-provider smoke checks using public sample text/audio.
- Build browser/macOS targets and report native GUI/microphone QA separately;
  simulated recordings are not evidence of a human pronunciation assessment.
