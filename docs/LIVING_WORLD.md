# Dynamic living-world stories

This source upgrade adds a separate story director to the Unity game. Existing café, family and city saves keep their own keys. It does not replace those worlds or rebuild the open scene.

## Play the first story

1. In Unity, choose **ContextSpeak → Living World → Prepare stories and activity district**. This creates the ScriptableObject catalog, actor prefabs and `LivingWorldDistrict` scene from existing assets. It preserves the open authoring scene.
2. Play the existing **ForestSlice** scene with a created character. Open **Life stories** at the top right. The first story is **A new face in town**.
3. Track Jordan, walk nearby, and choose what you want to communicate. The NPC starts speaking when the story opens. Choose **Record reply**, speak, then stop recording. Assessment runs automatically.
4. If your wording needs correction, listen to the corrected reply and record it again. A grammatical alternative with the same meaning is accepted. Typing is rehearsal and never completes a speaking turn.
5. Accept the evening invitation. In the journal, choose **Wait until evening**, then track **See you at the night market**. Recommend food and decide whether to join the trip.
6. If you accept, track **A change of plans** at the existing airport. Discuss the delay and request the morning alternative or refund information. These are fictional story outcomes, not real bookings or refunds.

Politely declining is a successful branch. Close saves the current turn and correction; the journal can resume it. Dismissed invitations have a cooldown. NPC facts, promises, connections and completed conversations appear in the journal.

Start the existing speech companion with `pnpm world:dev`. Use **Reconnect AI** if unavailable. Microphone recording always requires a click; failed or unassessed responses grant no progress.

## Included scope

| Feature | Status |
| --- | --- |
| Café → Night Market → Airport branching chain | Authored rules, source integration and local AI adapter implemented; full live Unity traversal pending |
| Contextual invitations | Location, earlier choices, opening time and rain; ordinary interactions take priority |
| NPC memory | Bounded authored facts, relationship value, promises and last encounter; confirmed speaking turns only |
| Learning mode | Required correction/retry, semantic alternatives, typed rehearsal without credit, stale-result protection |
| Difficulty | IELTS 3–9 practice targets; initial target follows the character profile, with a journal selector; three prompt styles and seven response goals |
| New activity areas | Seven compact walkable practice spaces: Night Market, Camping Ground, Detective Office, Pet Adoption Centre, Cooking Academy, Music Festival and Cruise Terminal |
| Side conversations | Original two-turn activities; camping requires rain, selectable for practice in the journal |
| World effects | Existing Meshy characters/props, talking/agreeing animations where supplied, rain preview and saved story outcomes |
| Saves | Versioned local PlayerPrefs JSON; per-event resume, bounded memory and exactly-once turn receipts; no account/cloud sync |

The seven areas are small practice pavilions in one district, not seven finished standalone maps. NPCs currently stay at their service posts. Full daily schedules, a social graph, festivals/calendar, procedural map generation, pet ownership and business-management simulation remain future work. Relationships do not yet feed a shared multiplayer story or the website Dashboard.

Scenario generation selects validated authored templates. The LLM checks language and semantic goals; it cannot invent executable actions or directly change money, inventory or permissions. Phoneme-level pronunciation assessment is not implemented here: transcripts cannot support acoustic pronunciation scores. Practice targets and connection values are not official IELTS results.

## Build and verification

Prepare the living-world assets **before** a player build. The existing café build now includes the ordinary activity/airport scenes alongside the bootstrap. ForestSlice remains Addressable and uses its existing loader; returning from an ordinary scene safely handles an already released Addressables handle. Rebuild both Addressables and the player to see source changes in browser/desktop exports. Existing exported games are not automatically updated by editing C#.

Pure regression runner, using .NET 8:

```sh
dotnet run --project tools/unity/living-world-checks
dotnet run --project tools/unity/slice-checks
pnpm exec vitest run packages/world/slice.test.ts
```

Unity integration fixtures: **ContextSpeak → Living World → Run isolated play checks**. The fixture refuses to replace dirty open scenes, backs up/restores the scene setup, character profile, story preference and existing café save file, and uses explicitly synthetic assessment responses. It checks JSON roundtrip, recording-result handling, mandatory retry, completion guards, HUD hide/restore, input restoration, active-scene player binding, and a full café → night market → airport → café traversal through the actual scene loaders. It does not test a human microphone or model accuracy.

Verified on 2026-11-13:

- 60 new core regressions pass, including the complete branching chain, all side activities, time/weather conditions, correction gating, invalid templates/actions, damaged turn recovery, activity switching, promise resolution and duplicate prevention.
- 335 existing café checks and 7 speech-review API tests pass.
- Unity catalog/district preparation succeeded. The current scripts compiled inside the open Unity Editor, and the isolated native integration check passed after fixing the test-runner assembly placement and modal HUD overlap.
- The current runtime and Editor C# sources compile against the installed Unity 6.3 compiler/references, with compiler outputs isolated from the open Editor's Library.
- Real local AI accepts a natural public sample answer and corrects a deliberately ungrammatical one. Local TTS → transcription reproduces a generated sample reply. This is synthetic audio, not a human-recording assessment.
- Native integration now verifies the complete three-location chain and return, including the released Addressables handle, without losing story progress or movement. Tests use explicitly synthetic reviewer responses, not fabricated human recordings.
- The dialogue was inspected in the actual Unity Game view. Ordinary HUD prompts are hidden while the story owns the modal, empty correction blocks are omitted, and the Record reply control is visible.
- A separate **Preview with live speech** menu opens an isolated real-companion microphone check after running the fixtures. Human microphone input is pending; rebuilt browser/Mac exports also remain unverified. No claim of a tested distributable is made.

Generated test evidence stays under ignored `.local/living-world/`. No API credentials are stored in source, catalog or story saves.
