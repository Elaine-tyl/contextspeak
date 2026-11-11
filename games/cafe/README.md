> Legacy source reference. Active game builds now use [games/unity](../unity/README.md). See [current setup](../../docs/GAME-ENGINES.md).

# ContextSpeak Worlds — local prototype

A single-player 3D speaking prototype with animated CC0 characters. Choose
shopping with a friend, a farm, a market, a restaurant or an amusement-park outing;
each has two roles and three conversation goals. The original cafe mission also
remains: order two drinks within $12, then sit down and chat with Alex.

NPCs speak using local AI voices. Stop a recording to check it automatically,
practise any correction and continue. Friend outings accept casual wording and
reasonable personal choices, with matching clothing and park cues.

The café is a timber-and-glass forest conservatory, with an original curved
tree, broadleaf canopy, trailing vines and a real 3D garden outside the windows.
Muted oak, sage upholstery, brass lanterns and cream/navy/brown outfits share a
restrained palette. Cached geometry and opaque instanced leaves, three glass
panes and one shadow-casting sun keep the GL Compatibility scene bounded.
The closer exploration camera follows gently and adapts to narrow views; the
counter and seated-friend closeups retain their framing. Reduced motion settles
the camera immediately and stops the small foliage sway.

On narrow browser panels, **Mission details** expands the existing instructions,
order and movement controls. The compact view leaves the NPC visible. Long
speech/corrections scroll with the mouse or Tab → Home/Down/End; **Esc** also
closes a conversation while editing the reply.

## Run on macOS

Prerequisites: the repository's Node/pnpm dependencies, Python 3.14+, Apple
command-line build tools, and Ollama running locally. No cloud API key is used.

```sh
pnpm install --frozen-lockfile
pnpm world:setup
ollama pull qwen3:4b
ollama pull phonon:31m-en
pnpm world:test
pnpm world:build
pnpm world:dev
```

Open `http://127.0.0.1:4320` to play. The website's Games → 3D cafe prototype
entry also opens this launcher. Keep Ollama and the companion running for both
the browser and desktop versions. Initial downloads require network access and
several GB of disk space; subsequent inference is local and uses CPU/RAM.

The bootstrap installs checksummed Godot 4.7.2, export templates, whisper.cpp
1.9.5, the English base model, and isolated build tools under ignored `.local/`.
The exact Qwen and Phonon model digests are checked at runtime in the providers;
if an upstream tag changes, use the pinned version or explicitly review an
upgrade. Setup does not install system packages or read `.env.local`.

## Play the original cafe journey

- Move with WASD/arrows, or press Space / Walk to counter.
- Near Maya, press E / Talk. Type a reply and press Enter, or click Record reply.
- Stop the recording to submit automatically; recordings stop at 20 seconds.
- NPC questions play aloud automatically. Hear replays the current line.
- Answer the temperature and takeaway questions, then explicitly confirm.
- After Maya finishes, carry your drinks to Alex and sit down automatically.
  Join Alex / Space also continues if voice generation fails.
- Alex starts a three-turn conversation. Give a drink preference and reason,
  describe a weekend plan, then invite your friend and suggest a time.
- Stop each recording to check it automatically; feedback stays visible after
  playback. Rejected answers keep the question. Refresh restores accepted turns.
- Ask for prices or repetition. If over budget, give a new complete order.
- Restart resets this guest mission. Closing the conversation cancels recording
  and pending requests. A typed fallback works if AI or microphone access fails.

## Jump to a stage for testing

At the top right, choose a **Practice stage**, then press **Select**:

- Continue saved journey: resume the normal saved mission.
- Counter ordering: start at Maya, ready to order.
- Friend preferences: start the seated drink-preference question.
- Weekend plans: jump directly to the weekend question.
- Invitations: test inviting Alex and proposing a time.
- Completion preview: inspect the finished state.

The five direct jumps are clearly labelled **Test preview · not saved**. They use
sample earlier replies only to set up the chosen checkpoint. They cannot overwrite
your real browser/native save. Restart repeats the selected preview; choosing
Continue saved journey or reloading returns to the original save. Selecting a new
stage cancels pending recording, speech and AI feedback. NPC speech starts after
an explicit Select when the local companion is ready. These are checkpoints in
the existing café mission, not newly invented difficulty levels.

Microphone use requires browser permission. Wait for the recording indicator
before speaking. The browser captures PCM in an AudioWorklet independently of
3D rendering; desktop uses bounded Godot audio capture. A meter and timer show
input activity. Silence is rejected before transcription. Failed checks retain
the order and restore keyboard input for retry or a typed reply.

Microphone data stays local. Raw audio is held in game memory,
encoded as bounded 16-bit PCM, then temporarily written with owner-only permissions for local Whisper and
removed after success/failure. Only validated order state, accepted conversation replies and feedback are saved: browser
localStorage or the native Godot user directory. Generated NPC WAV files are
reused from `.local/world-tools/audio` (up to 64 files / 64 MB). Clearing that
directory discards the audio cache. Guest progress is not synced to accounts.

## Builds and checks

`pnpm world:build` creates ignored outputs in `.local/world-builds/`: Web and an
unsigned macOS ZIP. The desktop ZIP is a development client, **not a standalone
installer**: it still needs the local companion and models. macOS may require
the normal system approval for an unsigned app. The bootstrap currently targets
macOS; Windows is only an export preset and has not been built or tested.

```sh
pnpm exec vitest run packages/world
pnpm exec playwright test --config playwright.world.config.ts
pnpm typecheck
```

Gameplay regressions run the real exported WebGL game, with model responses
stubbed where necessary. To exercise five synthetic microphone replies through
the actual local Whisper and Phonon models (no transcript fixture):

```sh
pnpm world:test:voice
```

This generates public synthetic WAV fixtures under ignored `.local/`, launches
Chromium with a fake microphone, checks browser NPC playback start/end, four ordering replies, one friend reply,
two subsequent typed conversation turns, and saved completion. A question must
not be mistaken for order confirmation. It requires the local models and
companion. This does not record a person's microphone.

For an optional native-GPU render smoke, run the following **without**
`--headless`. It renders the world/UI briefly, prints a capped frame-rate and
draw-call sample, and writes no learning progress:

```sh
.local/world-tools/Godot.app/Contents/MacOS/Godot --path games/cafe --script tests/render_profile.gd
```

The current appearance is original campus artwork: cartoon faces, hair, varsity
jackets/hoodies, backpacks and sneakers, attached to the verified free Quaternius
Animated Woman rig. The intended look is roughly mid-teen; this is a stylized art
direction, not an age guarantee. The licensed source mesh remains hidden; its
idle/walk/sit animation drives the original visible parts. KayKit furniture uses
an atlas-preserving cream/wood/sage palette shader, with pendant lights and a
separate friend-table seating area. See [assets/CREDITS.md](assets/CREDITS.md).

The free Universal Base Characters Standard archive was inspected: it contains
Superhero base glTFs, not the advertised Teen variants. No paid Teen/Source file
was used. This is why the campus appearance is original rather than a claim to
have imported the paid Teen mesh.

Cloned source builds work offline once runtime tools/models are installed: selected
art is vendored. `python3 tools/world/verify_assets.py` checks pinned source/license
hashes before exports. `python3 tools/world/test_verify_assets.py` checks tamper
rejection. No Blender or paid source pack is required. Headless tests validate
rigs/geometry; palette shader rendering is checked in the actual WebGL browser.

The scene camera moves closer at the counter and frames both seated characters
at the table. It respects the browser's
reduced-motion preference. There is no background music masking NPC speech.

A synthetic microphone verifies recording/submission;
it does not establish human recognition or pronunciation accuracy. Task scores
measure order completion, not accent, fluency, CEFR level, or vocabulary mastery.

## Design and boundaries

Godot owns movement, dialogue and deterministic mission rules. The loopback
companion handles bounded WAV recognition, strict-schema intent extraction and
cached local speech. Qwen may interpret a paraphrase and supply bounded conversational feedback; it
cannot set prices or award rewards. The game requires a completed order before
seating and three accepted answers before the friend conversation completes.
Clear task evidence corrects inconsistent model verdicts; other phrasing remains
model-reviewed. This is a prototype and can still misjudge novel answers. The companion listens only on 127.0.0.1,
checks host/origin/session tokens, limits concurrent jobs and cancels abandoned
requests. It is not suitable for public hosting without a separate deployment
and security design.

Next: human microphone/usability testing, more tasks and authored difficulty
progression across the connected scenes.

See [NOTICE.md](NOTICE.md) for third-party components.

Free art choices and license boundaries are compared in
[Free 3D assets](../../docs/FREE_3D_ASSETS.md). The integration and repeated-question
fix are recorded in [Free art playtest](../../docs/CAFE_FREE_ART_PLAYTEST.md). Earlier
journey checks are recorded in [Café friend playtest](../../docs/CAFE_FRIEND_PLAYTEST.md).

The current campus appearance, test-stage controls and verification are documented
in [Campus café playtest](../../docs/CAMPUS_CAFE_PLAYTEST.md).

## Scene and role practice

Use **Scenes** in the game header to choose Shopping with a friend, Farm, Market, Restaurant or Amusement park with a friend, then choose your role and **Enter scene**. Each setting has two roles and three short communication goals. The original After Class Cafe journey remains available in the same selector.

The five roleplay maps each have three connected areas and three named NPCs:

| Setting | Areas |
| --- | --- |
| Shopping | Clothes boutique, sale corner, fitting rooms |
| Farm | Crop fields, farm shop, packing barn |
| Market | Fruit stall, produce stall, market checkout |
| Restaurant | Welcome lounge, dining room, restaurant till |
| Amusement park | Ticket plaza, ride garden, snack square |

- Enter at the map entrance with no dialogue open. Move with **WASD / arrows**;
  the camera follows. Furniture, NPCs and the map edges have collisions.
- Approach an NPC and press **E / Talk** to hear them. A task marker and the
  mission panel show the next stop. **Space / Walk** follows a safe path there;
  manual movement cancels it. It does not teleport or start dialogue.
- You may explore any area and greet any NPC. Other actors give directions;
  task questions advance in order at their own locations.
- A passed answer plays the current actor's acknowledgement, closes the
  dialogue and highlights the next NPC. A correction stays with the same actor.
  **Esc / ×** closes dialogue and returns to exploration at any time.
- After all three goals, keep exploring or select another role. Resuming starts
  at the entrance with the role's goal progress and corrections restored.

Record your reply and stop: local Whisper transcribes it and local Qwen checks its meaning and wording automatically. You can also type. When a correction is needed, hear and repeat the model sentence before the next question. Case, punctuation, common contractions, spoken numbers and currency notation are normalized for this readback. It checks recognized words, not pronunciation. Assisted steps are labelled separately from independent answers.

Each completed goal creates a corresponding visible prop or effect. Each role has a separate device-only guest save, including pending corrections; scene changes cancel old recording, voice and assessment work. **Restart this role** resets only that role. These prototype results are not account Dashboard scores. See `docs/SCENE_ROLEPLAY_PLAYTEST.md` at the repository root for verification and limits.

Friend outings use casual English and accept short answers, contractions and reasonable alternatives. In shopping, choose your own clothes, discuss the price or color and make a plan. At the park, choose rides, reassure a friend, grab snacks or plan tickets/meeting points. Clothes and park cues reflect accepted answers and restore those choices with your role progress. The previous school lesson is replaced; its old save does not count as shopping progress.

## Real-life challenges: US and Canada

Open **Scenes**, choose a setting and role, then choose **Real-life challenge ·
US & Canada** and **Enter scene**. For the forest café, leave the setting on
After Class Cafe and select the challenge. Everyday still opens the original
order-and-friend journey.

Challenges contain 33 goals across 11 roles: shopping, farm, market, restaurant,
amusement park and the forest café. Each goal has an opening and a follow-up
with the same NPC. Explain choices, handle an unavailable item, clarify a mix-up,
work within a budget or arrange a backup plan. Both exchanges must pass before
the goal completes and its scene action appears. Stop recording to submit as
usual, or type a reply.

Say **“Could you say that again?”** to hear the question without a failed answer
or a progress change. A correction applies only to the current exchange; an
assisted opening still needs its follow-up. Each difficulty saves separately,
including unfinished follow-ups. Old role saves continue as Everyday progress.

AI review checks two authored requirements and validates quoted evidence from
the current reply. Other natural choices and regional English remain valid;
examples are suggestions rather than mandatory answers. The model can still
misjudge meaning. It gives wording/task feedback, not validated pronunciation,
accent, fluency or proficiency scores.

These are original fictional US/Canadian situations. Currency is identified in
the brief; quoted prices include any tax for the exercise. Stock, return rules,
time limits and other venue conditions are stated scenario facts. They are not
national customs or actual business policies. The local English voice is not
an assurance of a specific US or Canadian accent.

The communication functions were informed by the US Department of State's
[Everyday Conversations](https://americanenglish.state.gov/resources/everyday-conversations-learning-american-english)
and the British Council's [speaking practice](https://learnenglish.britishcouncil.org/free-resources/speaking/b1).
Toronto's [visitor transport information](https://www.toronto.ca/explore-enjoy/visitor-toronto/getting-around-the-city/)
supports the streetcar setting. Their dialogues, audio and images are not copied.

See the [challenge playtest notes](../../docs/REAL_LIFE_CHALLENGES_PLAYTEST.md)
for verified flows, screenshots, reproduction commands and pilot limits.

## 2D café preview

Open [the 2D version](http://127.0.0.1:4320/?view=2d) after `pnpm world:dev`.
The illustrated café uses original native vector art: garden windows, wooden
floorboards, green upholstered chairs and animated characters/cup steam.
Use WASD/arrows to explore, E to talk, or Space to walk to the next stop.
Order with Maya, then join Alex at the table. Local AI voice, recording,
automatic checking, corrections and practice-stage previews reuse the same
gameplay code. Scenes offers the café's Everyday and US/Canada challenge.

2D progress uses separate browser keys/native save files. Return to the
[3D version](http://127.0.0.1:4320/) to continue its existing journey or other
maps. The macOS package also accepts `-- --2d` on its executable. This first
preview covers the café only and shares the existing Web asset package.
See [2D verification](../../docs/CAFE_2D_PLAYTEST.md).
