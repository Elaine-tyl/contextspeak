# ContextSpeak Life V8.0 — Living NPC World (first complete neighbourhood)

Build: **ContextSpeak › NPC › Build Living NPC System** → `Assets/ContextSpeak/Scenes/LivingNeighborhood.unity` (starts 08:06).
Checks: **ContextSpeak › Checks › Living NPCs** (edit mode) and **› Living NPC Play Mode Tests** (writes `Logs/LivingNPCPlayModeReport.md`).
In game: **F6** NPC inspector (`[` `]` select, time/rain/event/animation/path/anchor tools) · **F7** crowd overlay · **F5** swimming · **E** talk / help.

## Architecture (hybrid, cheap by default)
| Spec component | Implementation |
|---|---|
| NPCIdentity / NPCPersonality / voice / band | `NPCProfile` asset (`Data/LivingNPC/Profiles`) |
| NPCDailySchedule | `NPCDailySchedule` — the designer JSON array (`startHour/endHour/activity/location`), per-person punctuality offset |
| NPCNeeds / NPCUtilityAI | `NPCNeeds`, `NPCUtilityAI` (schedule vs hunger/fatigue/social/rain; hysteresis + commitment) |
| NPCBehaviorExecutor | `LivingNPC` programs (coroutine HSMs): barista/cashier shifts, café customer, shopping, tram commute, school run, home life, tourist, errands, shelter, rest |
| NPCNavigationController | NavMeshAgent wrapper: speed variation, jogging, stopping distances, avoidance, red-light holds at crossings (road is non-walkable except zebras), doors |
| NPCAnimationController | AuthoredMotion clips (speed-matched walk), retargeted sit/wave/talk/look-around, TEMPORARY procedural everyday actions + props, gaze with head-turn limits, breathing, weight shifts |
| NPCPerceptionSystem | staggered (4 NPCs/frame) proximity + FOV + `WorldEvents` (drops, noises, rain) — short glances with cooldowns, never staring |
| NPCSocialBrain | friend greetings/chats with cooldowns, walking together (`WalkWith`), sitting with friends (seat groups) |
| NPCRelationshipMemory | `living_npcs.json`: relationship, encounters, facts (e.g. preferred drink, "helped"), summaries, NPC world state |
| NPCConversationController | `NeighborhoodFlow` → existing `ScenarioManager` (Free / Guided with mandatory corrected repeat / mock exam setting unchanged) |
| NPCEventReactionController | `NPCEventDirector`: bump + dropped item, tourist asks directions, familiar greetings, weather remarks — accept with E or walk away |
| NPCLifeSimulationManager / NPCCrowdLODManager | `NPCLifeSimulationManager`: tiers 1/2/3, budget, logical off-screen travel without visible jumps, saving |
| Transport | `LivingTram` (doors, reserved seats, riders parented), `LivingCar` (stops at lines), `LivingCrossing` (signals) |

## Not available (reported honestly)
- No facial blendshapes / eye bones on the Meshy models → no blinking, saccadic eye movement or visemes (head gaze only).
- No authored clips for drink, eat, phone, read, type, cook, wipe, umbrella, photo, lie down → procedural TEMPORARY poses.
- Legacy Animation has no foot IK → ground contact via speed-matched walk cycles and NavMesh height.
- Restaurant, mall, zoo, airport, beach contexts: the framework applies, but only the neighbourhood is set up in this pass.
- Visual quality is not validated until the scene is rendered and inspected.
