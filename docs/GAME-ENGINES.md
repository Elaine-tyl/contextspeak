# Game engines

**Unity 6000.3.28f1 LTS + C# + URP17.3.0** runs the 3D Life world and cafe in
`games/unity`. **Godot 4.7.2 + GDScript** runs the eight 2D learning games in
`games/arcade`. This split follows the latest user request and supersedes the
earlier all-Unity arcade decision. Engine versions and dependencies are pinned.
The historical Godot 3D world and Unity arcade source are retained for reference.

The website/extension remain TypeScript. They own login, vocabulary, trusted
assessment and account learning history. Migrating game rendering does not
replace these services or expose credentials to a game binary.

## Setup and play

Install and activate Unity6000.3.28f1 arm64 with **Web Build Support** on macOS.
The build script checks the pinned executable; `CONTEXTSPEAK_UNITY_EDITOR` can
specify the same version at a different path. The installed macOS Mono player
builds without an additional Xcode/IL2CPP toolchain.
Apple's Command Line Tools (`xcrun clang++`) compile the small macOS microphone
permission bridge during `unity:prepare`. No downloaded native binary is needed.

```sh
pnpm install --frozen-lockfile
pnpm world:setup        # pinned local Whisper; no Godot download
pnpm speech:setup       # pinned local Phonon speech model
pnpm unity:prepare     # verify/copy CC0 art, configure URP
pnpm unity:test        # mission, navigation, arcade and native UI checks
pnpm world:build       # Unity Web + bundled districts
pnpm unity:build:mac   # Unity macOS preview + ZIP
pnpm arcade:setup      # verified Godot engine and Web templates (macOS)
pnpm arcade:test       # native Godot board regressions
pnpm arcade:build      # account-connected Godot 2D Web export
pnpm world:dev         # local world + speech companion on 4320
pnpm dev               # website, accounts and learning API
```

Ollama must also be running with the model specified by the local provider.
Open `/world` on the learning website, or
http://127.0.0.1:4320/unity/?world=life directly. The companion root opens Life;
`/unity/` opens the preserved original cafe. `/download/macos` serves the Unity
ZIP. Build outputs, model downloads, private audio and Editor caches are ignored.

In Unity Hub, add `games/unity`. Run **ContextSpeak → Rebuild cafe scene**;
open the generated ForestCafe or LifeTown scene and Play. Generated scenes and
art are reproducible; edit their Editor builders rather than generated files.
The desktop preview requires the local companion and models; it is not yet a
self-contained installer or an account-connected desktop release.

## First playable day

Four NPCs and twelve spoken goals form the first complete loop:

1. Mom gives a dinner-shopping errand at home.
2. Walk to Sam, ask for three apples and two carrots, then pay $7.
3. Deliver groceries to Mom; dinner appears and the ingredients leave your bag.
4. Order a drink from Maya, select temperature and here/to-go, then explicitly
   confirm to receive the drink and a correctly priced paid receipt.
5. Catch up with Alex: preference with a reason, a weekend activity and an
   invitation with a time.

Start with $20. Menus, wallet, bag and visible rewards reflect earned progress.
WASD/arrows walk; E talks; right-drag looks; scroll zooms; Esc explores.
**Walk to objective** follows a complete NavMesh route. Movement keys cancel it.
Test-stage jumps are marked practice and cannot replace an earned save.

NPC questions use local AI speech. Stop recording to transcribe and assess
automatically. A real correction holds the current goal until a new correct
spoken reply. Equivalent natural wording is accepted. Typed replies in Life
are rehearsal. A transcription failure is retryable and does not lose progress.
The original cafe mission retains its earlier typed-reply behavior.

On macOS, allow the microphone permission sheet when first recording. The game
keeps the conversation open for that sheet; normal focus loss still cancels
recording. Denied access, no input device and timeouts have retryable messages.
The desktop export check rejects a build without its microphone bridge.

Older Godot `user://` cafe/roleplay save files are not imported.
The existing Unity and account saves are preserved.

Life saves keep `contextspeak-life-day-v1`; original cafe saves keep
`contextspeak-unity-cafe-v1`. Life remains local guest progress, separate from
account arcade marks, vocabulary recall and acoustic pronunciation assessment.

## Reusable framework

| Area        | Implementation                                                         |
| ----------- | ---------------------------------------------------------------------- |
| Controls    | CharacterController and shared Input System action map                 |
| Camera      | Cinemachine follow/aim; no competing camera-transform writer           |
| Navigation  | Baked NavMeshSurface, complete paths only, extra corner clearance      |
| NPCs        | Capability ScriptableObjects, conversation FSM and Animator states     |
| Missions    | JSON catalog exposed through a ScriptableObject; pure C# rules         |
| Voice       | Shared recorder, loopback client and cancellable NPC audio player      |
| Districts   | Local Addressables, additive load, unload previous scene after success |
| UI          | UI Toolkit on desktop; semantic HTML world controls on Web             |
| Persistence | Validated local JSON in existing save slots; no new database           |

Closing conversations, recording, focus loss and scene changes invalidate late
AI/audio work. Tokens stay in memory. API keys and microphone bytes never enter
saves. Transcript/grammar feedback is not an acoustic pronunciation grade.
Animator states provide restrained idle/conversation motion, not full lip sync
or autonomous daily schedules.

The first district set is home/market/cafe. School, farming, try-on clothing,
restaurants, fishing, amusement parks, apartments, civic services and international
travel remain future content releases. Empty folders/scenes do not represent
finished features. See the [unification design](superpowers/specs/2026-11-11-unity-unification-design.md).

## Learning arcade

Open http://127.0.0.1:3000/games and sign in. The real Godot 2D
board presents the existing eight modes and ten-level catalog: dictation,
missing words, picture choice, listening questions, spoken responses, puzzle,
riddle and clearly labelled simulated AI PK.

The website owns account I/O, audio and microphone recording. The frame receives
only the current question, controls and result. Same-origin/exact-frame/current-round
checks reject stale actions. Scores and unlocks still come from the existing
trusted backend; there is no arbitrary-score API. Pending submissions retry the
same event, and reloading restores the account-owned run. Detailed corrections,
red highlights and IPA remain in the accessible website feedback below the board.

If the export is missing or WebGL is unavailable, continue the same run using
accessible controls at `/games/web`. `/games/arcade` remains a compatible Godot
alias. Native account arcade login/pairing is a future feature;
no extension credentials are reused in a desktop game.

## Verification

- `pnpm test`, `pnpm typecheck`, `pnpm format:check`, `pnpm build:web`.
- `pnpm unity:test`: C# mission/payment/save/correction rules, complete routes,
  capsule clearance, arcade gating and real native UI layout/draft retention.
- `pnpm test:unity`: exported cafe/Life browser journeys with fixture AI and real
  browser PCM capture. Fixtures do not create learner account scores.
- After `pnpm arcade:build`, `pnpm exec playwright test tests/e2e/arcade.spec.ts`:
  account-owned Godot rounds, keyboard input, recording, scoring and fallback.
  CI explicitly skips engine-only journeys when no ignored export is present.

Automated builds and simulated microphone journeys must be reported separately
from a human desktop microphone/visual playthrough. OS permissions and an unlocked
Mac are required for that final pilot check. Current evidence and any pending
checks are recorded in the unification implementation ledger; no public pilot or
GitHub push is implied by a successful local build.

Verification snapshot (2026-11-11): 567 source/database regressions, one build-tool
regression, C# mission/navigation/arcade checks and three native UI layout checks
passed. Fourteen exported world journeys and sixteen Godot/HTML game journeys
passed, covering eight modes,
real browser capture with fixture assessment, marks, retries, stale frames,
responsive layout and missing-export fallback. Web, macOS and production website
builds, ZIP integrity, typecheck and formatting passed.

After the Mac was unlocked, the actual desktop player was opened and visually
checked: keyboard walking, a cafe-to-Life scene switch, guided walking, NPC
interaction and real local-AI typed rehearsal worked. Rehearsal correctly stayed
at the first spoken goal. A real recording attempt exposed unsupported macOS
authorization; a native AVFoundation bridge, cancellation/timeout checks and an
exported-library guard now cover that path. Refreshed browser capture, permission
cancellation and correction-retry checks passed (3/3). The OS requested permission;
the human microphone assessment remains pending permission confirmation and a
recorded reply. Build checks and fake audio do not certify pronunciation.

The compact conversation card uses a 620-pixel maximum width and a bounded
scrolling area. Long feedback stays readable without covering most of the world;
recording and typed-reply controls retain their existing behaviour.
