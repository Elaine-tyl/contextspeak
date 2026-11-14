# Development progress — ContextSpeak Life locations

## Implemented
- Shared systems: InteractableObject, InteractionManager (sit/stand, use while seated), DoorController
  (sliding + hinged, lockable), SeatController, NPCController (schedule, errands, NavMesh), NPCDialogueController,
  PortalDoor, ScenarioManager, DialogueStateMachine (band variants, hints, model answer, repeat gate),
  SpeechEvaluationManager (local companion STT/TTS, slow playback), QuestManager, InventoryManager,
  CurrencyManager, MenuController, PaymentController (card/cash, tip, split), SceneTransitionManager
  (return to the door you used), SaveManager (PlayerPrefs JSON).
- Hospital.unity and Restaurant.unity built by one menu, with full flows described in README.
- Forest Café upgraded in place; café orders feed inventory + XP; train stop back to the city.
- Metro City: E at the hospital / restaurant doors and the station loads the location; return point restored.
- 12 editable scenario assets, 2 editable menu assets.
- LifeLocationChecks: band variants, model answers satisfy meaning, wrong answers and wrong repetitions do
  not advance, skip rules, cart/customisation maths, wallet, inventory, save/load, return point, quest XP,
  MOCK typing, scene wiring (NPCs, doors, seats, tables, portals, NavMesh).

## Mocked / simplified
- MOCK MODE when the free speech companion is offline: typed answers, clearly labelled, never counted as speaking.
- Pronunciation is not scored (a transcript cannot judge it); criteria notes come from SpeakingCoach.
- UI is IMGUI (no prefabs needed) instead of uGUI/TextMeshPro; dish "photos" are coloured plates with a label.
- Food is simple primitives on plates; eating is press-E bites (no eating animation in these scenes).
- Medical content (cases, medicine label, advice) is fictional practice text, not medical advice.
- Payment uses fictional money ($100 start).

## Not done yet / next
- Not yet compiled or play-tested in Unity by the developer of these files: run the build menu, then the checks.
- Character art: if a Meshy model is missing a capsule placeholder is used (listed in the Console report).
- NPC conversation animation (gestures/lip-sync) and seated animations for NPC diners.
- Multiplayer rooms are only wired in Metro City / Forest Café, not inside the hospital and restaurant.
- uGUI/TMP versions of the panels, localisation of UI text, accessibility pass.

## Sky & weather (added)
- Implemented: URP dynamic sky shader, SkySystem (time, sun/moon, clouds + cloud shadows, ambient, fog, exposure,
  reflections, rain, wetness), 4 weather profiles, 7 presets, smooth blends, Forest Café WorldClock sync (night enabled,
  `AllowNight` switch restores daytime-only), street lamps, headlights, interior/window lights, crowds, NPC sheltering,
  terrace closure in rain, water/road response, weather audio, F2 panel, installer, test-shot renderer, SkyChecks.
- Not available: no HDRI files are bundled (downloads blocked here) — the HDRI slots are optional. No farm exists in the
  project yet, so there is nothing to wire for farming; `SkyState` exposes what a farm would read (rain, daylight).
- Visual verification: only after the test shots in `SkyTestShots/` have been rendered and looked at.

## International airport (added — NOT yet compiled or played in Unity)
Implemented (code + scene builder):
- 3 scenes, connected by TravelPortal; Metro City station → Airport Express → Airport.
- Travel data: 5 destination packages (signs, currency, hotel, flight time), bookings, baggage allowance and fees,
  bag tags, boarding-pass validation (7 states), currency exchange, security tray mini-game, 17-step quest with
  prerequisites, all saved inside LifeProgress.
- 22 airport conversations with Band 3–4 / 5–6 / 7–8 lines and Band 9 lines for the IELTS practice; destination,
  purpose, stay and hotel answers are validated against the booking; repeat gate from the shared dialogue system.
- Panels: FlightBooking, FlightInformationBoard, AirportMap, CheckIn + SeatSelection, BaggageCheck, Security,
  PassportControl, BoardingPass, GateInformation, AirportShop/RestaurantMenu (shared menu), Exchange, BaggageClaim,
  TaxiBooking, in-flight map.
- Blender assets: SkyLark Air airliner (A320-class size), baggage tug, fuel truck, pushback tug (Assets/Art/airport).
- Airfield: runway, taxiways, lights, control tower, an aircraft that lands, taxis, parks and takes off on a loop,
  moving tug, road traffic and a bus using the existing city vehicle models.
- Audio: synthesised crowd murmur, chimes + spoken announcements, scanner beeps, conveyor hum, café chatter,
  cabin ambience and engine levels. Footsteps reuse FootstepAudio.
- AirportChecks (EditMode): quest order, booking, fees, boarding states, security, exchange, every scenario for
  every destination, itinerary rejection, repeat gate lock/unlock, Band 9 lines, save/load, scene wiring.

Placeholders / simplified:
- Destinations are an arrival airport + hotel lobby, not explorable cities. Taxi/bus arrival at the departure
  airport exists (taxi spawn) but Metro City only links the train.
- NPCs: idle, walk, queue shuffle, turn to the player and talk; no gesture or work animations yet.
- Food, bags, trays and products are simple shapes; shop "photos" are coloured tiles.
- Flight is a seated sequence (ground drops away, time skips), not flight simulation. Cabin camera may clip walls.
- Missing models become magenta boxes named "PLACEHOLDER · …" and are listed in the Console.
- No escalators, no rolling-luggage sound.

Not done (performance items from the spec): Addressables for these scenes, occlusion-culling bake, LOD groups,
baked lighting (realtime with a small light budget is used), NPC update throttling, crowd-density setting.
No PlayMode tests exist yet — the full journey must be played once by hand.

## Tropical beach resort (Palm Bay) — 2026-11-13

Menu: **ContextSpeak > Build Tropical Beach Resort** → `Scenes/TropicalBeach.unity`; checks: **ContextSpeak > Checks > Beach**.
Reached from Metro City Central Station ("Coastal Line" entrance — rebuild Metro City to add it).

Implemented (code written; compile and scene build NOT yet verified in Unity at the time of writing):
- Procedural island terrain (sand, wet sand, grass, rock, path layers with generated albedo + normal maps), headland and reef shelf.
- Gerstner ocean (`ContextSpeak/Ocean`: depth colour, refraction, Fresnel reflection, sun glint, shore and crest foam; needs camera depth + opaque textures, enabled on the scene camera), waves follow wind/rain.
- Wind-animated palms and plants (`ContextSpeak/Foliage Wind`), culling LODs, 26 Blender-made GLBs in `Assets/Art/beach` (script `tools/blender/beach_assets.py`).
- Café/juice bar, Coral Kitchen (host → indoor/deck seating → waiter → cooking timer → bill + tip), beach club, souvenir stalls,
  watersports centre, dive hut, lifeguard tower and flags, marina + pier, sunset point, station, Palm Bay Resort (reservation, check-in, 4 rooms + 3 villas with key-card doors, sleep, room service, check-out, pool).
- 12 activities with booking, payment, safety briefing and weather/daylight closures: swim (buoy), kayak/paddleboard/jet ski (rideable craft + buoy courses),
  snorkel/scuba (swimming, breath/tank, underwater fog, 5 fish species to observe and photograph), surf (balance mini-game), pier fishing (timing), banana boat/parasail/boat tour/yacht (guided rides).
- Quest BEACH_DAY_001 (12 objectives) + 6 optional quests, 18 IELTS-banded conversations (Band 9 lines in the tourist chat).

Not done / placeholders: no swim, sit or gesture animations (swimmers use walk/crouch); outfit change is a labelled placeholder (no outfit models);
no water caustics, baked lighting, occlusion culling or terrain streaming; pool is decorative; ambience audio is the generic sky audio.

## Immersive Toolkit UI — 14 November 2026

Implemented a shared warm theme, safe-area/scaling foundation, persistent accessibility settings, minimal café HUD, actual overhead camera map, phone apps and Map/Speaking/Life tabs. Café subtitles and correction/repeat feedback are separate compact surfaces; the receipt menu retains voice ordering. Pooling covers prompts, toast slots and map pins. Phone/dialogue fades respect reduced motion. Legacy and city runtime presenters now use Toolkit, with modal ownership and one-level Back handling. Browser DOM gameplay overlays no longer compete with native UI; the character and microphone bridges remain.

Native foundation/assets, legacy controls/nested-modal and focused speaking/order/payment/route checks passed. Play-mode evidence: `UIShots/20261113-221542/acceptance.json`, 80 exact-size day/night captures, no native layout/font failures; resting HUD about 4%. Sample rendered screens were inspected, including Chinese glossary content. Fixtures are labelled synthetic and do not count as provider or human recording evidence.

Outstanding: physical phone/hand animation, newly authored world menu boards and payment camera close-ups, every legacy district's visual pass, hardware gamepad and new native-player QA, maximum-scale layouts and profiling. Current saved world art was preserved. See repository `docs/IMMERSIVE_UNITY_UI.md` for exact scope and reproduction steps.
