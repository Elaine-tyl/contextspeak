# Metro City Wildlife Zoo — phase 1

Build: **ContextSpeak > Build Wildlife Zoo** → `Assets/ContextSpeak/Scenes/WildlifeZoo.unity` (then rebuild Metro City for the Zoo Line door at Central Station). Checks: **ContextSpeak > Checks > Zoo**.

## What is in phase 1
- Station, entrance plaza, ticket office (standard / student / family), ticket gate, information pavilion (map, quiz), signposted hub, Savannah View Café, tram stop (closed), "opening later" notices.
- African Savannah on a sculpted terrain: waterhole, rocky kopje, dry/green ground, shade trees, reeds, post-and-rail perimeter, three viewing decks with information boards.
- Animals (`Runtime/Zoo`): `AnimalController` + `AnimalBehaviorStateMachine` + `AnimalNeeds` + `AnimalMovement` + `AnimalAnimator` + `AnimalHabitat` + `AnimalLODController` + `AnimalAudioController` + `AnimalInteraction`.
  Elephants (two adults and a calf that follows its mother), antelopes, a rhino; giraffes and zebras are box placeholders; a small children's farm (sheep, pony, pig, chickens).
- Quest `ZOO_VISIT_001` (14 objectives). Penguins, aviary and tram are skipped automatically and listed as skipped until those areas exist. Optional: photos (P), quiz, vocabulary hunt, conservation volunteer, lost visitor.
- Speaking: the shared dialogue machine (meaning-based acceptance → feedback → corrected sentence → repeat gate). Practice feedback only, never an official IELTS score; pronunciation is "not assessed" unless the companion provides it.

## Animal models
`tools/blender/zoo_animals.py` (run inside Blender): straightens and scales each Meshy model to real size, decimates it, builds a quadruped skeleton from measured landmarks, skins it and exports `Assets/Art/zoo/<Name>.glb`. `AnimalAnimator` drives the bones in code (walk cycle from distance travelled, graze/drink, trunk, ears, tail, looking around).
- Rhino and Tiger were modelled mid-stride, so one hind leg skins badly: they use `RigidLegs` (no leg swing) for now.
- Needed downloads: giraffe, zebra, antelope/gazelle, acacia tree (standing, side-on, legs apart). Animal sound recordings.

## Not tested yet (needs a Play run in Unity)
Compilation in Unity, the scene build itself, walking/animation in motion, feet on slopes, ticket payment and gate, café ordering, photos, visitor schedules, save/load in play, frame rate (no profiler run — the 60 FPS target is unverified).

## Later phases
2: rainforest, big cats (tiger model is ready), penguins, aviary, restaurant. 3: safari tram (tram and rail models are downloaded), reptile house, full farm, conservation centre. Zones are separate `Zone · …` roots so they can move to additive scenes.
