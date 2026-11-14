# Proactive NPC interactions — V7.1

## Play

The new encounter system defaults to `proactiveNPCFrequency: high` and `speakingMode: guided`. Existing V7 learning settings and saved quests are retained. Press **N** or click **N · NPCs** to choose Off / Low / Normal / High, switch speaking mode, reconnect speech or resume an encounter.

An available nearby NPC can approach and speak a small invitation. Press **E/F** or choose **Respond** to accept, choose the microphone action to accept and record, decline, or continue walking. The invitation expires after 12 seconds or when you walk away. Declining/ignoring has no relationship penalty and opens no full dialogue. Ambient greetings/weather remarks use a short subtitle and existing TTS without opening a dialogue.

A bump requires a real CharacterController collider hit, closing movement, a valid contact direction and an unobstructed reaction area. Standing close to someone does not manufacture a collision. Ordinary navigation retains its existing NavMesh avoidance; the proactive adapter yields near the player. No impulse is applied to either character.

## First events

- **City accident:** respond to the apology, offer help with a dropped map, explain directions, then meet the same acquaintance at Forest Café. Noticing the map and asking whether the NPC is okay select different authored follow-ups. The returning encounter supports recognition and an invitation.
- **Café spill:** respond to a spilled coffee, check welfare and suggest a safe clean-up. Small temporary props have colliders and bounded low-mass physics. No drink is purchased or removed from the player's inventory automatically.
- Original contextual encounters also cover School, Home, ShoppingMall, Supermarket, Airport, Restaurant, Hospital, Beach and Zoo. These shorter branches share one engine; they are not equally elaborate multi-step simulations. Scene aliases include named city actors such as `cafe:Maya` and `school:Reed`.

Events reuse existing actors, scenes and art. A returning visitor may be instantiated from that NPC's existing model resource when crossing to another saved scene. If that model is unavailable, the game logs the missing dependency rather than inventing an actor or silently granting progress. Without a baked reachable NavMesh, the obstacle-checked straight-line fallback can stop at obstacles; it cannot guarantee a route through a building.

## Speech and learning

The recorder, local speech companion, structured semantic/language review, TTS and V7 learning policies are reused. Guided corrections require a successful spoken repetition followed by hidden independent recall before the event advances. Natural equivalent expressions are accepted by the same semantic check as V7. The first independent meaning selects a branch; repeating a model cannot overwrite what the player originally noticed.

Free Life keeps corrections optional, while deterministic event steps still require an assessed, relevant communication goal. Mock Exam selection suppresses unsolicited encounters; the existing V7 exam centre remains the normal entry. The encounter adapter also retains deferred three-part rehearsal support. Missing services show **MOCK / unassessed**, retain the event and award no evidence or quest progress.

Pronunciation remains **not assessed** unless an actual acoustic assessor is supplied. Reference playback and recording replay are available; hidden recall conceals correction, transcript, feedback and model replay. This is formative practice, not an official IELTS result.

## Safety, memory and frequency

One accepted event at a time. High frequency uses a 25-second global minimum; Normal 60 seconds; Low 150 seconds. NPC cooldown defaults to 90 seconds and event cooldown to 180 seconds. Scene density reduces interruptions in hospitals, airports and restaurants. Profiles can further adjust NPC frequency, availability hours and eligible events. Cooldowns use UTC timestamps so restarting cannot bypass them.

No unsolicited conversation during a mock exam, another conversation, recording, transaction/menu, swimming/vehicle override, cutscene or conflicting story. Existing automatic proximity invitations yield to this director; daily schedules, existing manual stories and other gameplay remain available.

Only the authored `EventQuestAdapter` advances an event or awards bounded relationship credit. AI output has no currency, inventory, quest command or scene-transition fields. New event progress is separate from legacy quests. Relationship facts are mirrored through the existing NPC relationship API; no arbitrary inventory/currency reward is implemented.

The additive `proactive_npc_v71.json` sidecar uses temporary writes, atomic replacement and a recoverable `.bak`. It stores settings, bounded game-fact summaries, cooldowns, chain state and V7 learner evidence/pending correction. Raw recordings stay in memory and are discarded on close. New assessed event history is merged into the existing Slice dashboard without replacing its vocabulary schedules or wallet. Cross-district event learning state lives in the sidecar; cloud sync is not added.

## Components and authoring

`Assets/_Game/Proactive/Core` contains contact policy, contextual selection, cooldowns, memory, relationship and event transitions. `Runtime` contains the director, ProactiveNPCController, NPCInteractionDetector, NPCPlayerCollisionInteraction, NPCReactionAnimator, NPCConversationInitiator and ProactiveInteractionUI.

**ContextSpeak → NPC → Setup Proactive Interaction System** inspects existing active-scene NPCs, adds components/non-trigger bodies, creates reusable event/profile ScriptableObjects, retires the older proximity-bump director and saves only the active saved scene. Scene component edits support Undo; new reusable content assets remain. Imported models are not overwritten. Missing authored gestures/NavMesh/speech dependencies are reported.

**ContextSpeak → NPC → Create Proactive Interaction Test Scene** creates a separate capsule contact fixture. It is not replacement production art. Use existing world NPCs for supplied-rig and real-navigation checks.

Gestures reuse the existing NPCAnimationController/AuthoredMotion pipeline. When suitable authored gestures are absent, the existing procedural gesture fallback is used. No new motion-captured animation or acoustic assessor is claimed.

## Verification status

Browser release **20261114.114441** is available at `http://127.0.0.1:4320/unity/?world=slice` and `?world=city`. It was built from an isolated project copy, preserving the hashes of all 14 saved authoring scenes. The original Unity project contains the new components, reusable event assets and separate contact test scene. The existing downloadable macOS build was retained, not rebuilt for V7.1.

Executed verification:

- 50 proactive behavioral checks, including negative cases for proximity, wrong repetitions, irrelevant answers, cooldown bypass and unknown venue fallback.
- Existing V7: 36 checks; Slice: 335; Living World: 60; recording/bridge/pointer JavaScript: 8.
- Full web regression suite: 621 tests across 85 files; TypeScript type checking passed.
- Unity native integration checks and Web build passed. Native checks cover JSON defaults/save state, legacy wallet preservation, actor setup and overlay priority/picking. A temporary UI test fixture was corrected so it does not leak default InputActions into other fixtures.
- Browser: final build loaded in Forest Slice and Metro City; N opens the settings, High / GuidedLearning is visible, mouse Close works and movement resumes. The UI sorting issue found during playtesting has a native regression check. Screenshot: `/private/tmp/contextspeak-v71-settings.png`.

Remaining manual verification: a complete real-microphone tourist/spill encounter, cross-scene NPC rendezvous and NavMesh routing on supplied rigs, interruption checks while driving/swimming, macOS V7.1 runtime and measured frame-rate profiling. Nearby NPC approach was observed, but a full live invitation/accept/voice completion was not verified. Passing policy tests does not substitute for these playthroughs.

Legacy First Day LifeTown dialogue remains available through its existing system. New runtime actor setup targets LearningNpc, LivingNPC, NPCController and CityPedestrian actors. Other contextual branches are short authored interactions; there is no full missing-pet quest or unrestricted AI life simulation. Speech dependencies can be unavailable and then remain explicitly mock/unassessed. No acoustic pronunciation scoring was added. The pre-existing Python migration check concerning `VillaHome.cs` remains outside this change.
