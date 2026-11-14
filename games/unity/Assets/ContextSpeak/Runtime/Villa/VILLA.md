# Luxury Villa

A walk-in, three-storey modern villa with a passenger lift (~650 m² of floor plus ~230 m² of roof terraces, ~1,500 m² plot) built from the Meshy reference
"Contemporary Luxury Villa with Glass". The reference is a single closed shell (1M triangles, no
interior), so it is kept only as a hidden reference inside the .blend. The villa was rebuilt as
modular, editable architecture that keeps the reference's design language:

* cream plaster boxes, flat roofs, white parapets
* dark vertical wood slats
* a tall gold-tinted glazed stair tower at the front right
* a front balcony with a glass railing over the garage
* a black sliding gate between stone piers
* a front lawn and a cobbled driveway

## Files

| What | Where |
|---|---|
| Generator (Blender 4/5 or the `bpy` module) | `tools/blender/luxury_villa.py` — `python3 luxury_villa.py -- <out> [reference.fbx]` |
| Editable Blender file | `ArtSource/LuxuryVilla/LuxuryVilla.blend` (collections Architecture / Interior / Doors / Exterior / Reference) |
| FBX exports | `ArtSource/LuxuryVilla/LuxuryVilla_{Architecture,Interior,Doors,Exterior}.fbx` (+ `.fbm` texture folders) |
| PBR textures (BaseColor / Roughness / Normal, procedural) | `ArtSource/LuxuryVilla/Textures/` |
| Unity models (glTFast) + manifest | `Assets/ContextSpeak/Art/Villa/LuxuryVilla_*.glb`, `LuxuryVilla_manifest.json`, `RainUmbrella.glb`, `PastelParasol.glb` |
| Scene | `Assets/ContextSpeak/Scenes/LuxuryVilla.unity` (ContextSpeak > Villa > Build Luxury Villa) |
| Runtime | `Runtime/Villa/` — VillaDoor, VillaFixture, VillaRoom, VillaElevator (+ VillaElevatorCall), UmbrellaCarrier, VillaLighting, VillaPet, VillaState, VillaDirector, VillaFlow (+ VillaScenarios), Tests/VillaPlayTest |
| Validation | ContextSpeak > Checks > Luxury Villa (`Logs/LuxuryVillaValidation.md`), … > Luxury Villa Play Mode Tests (`Logs/LuxuryVillaPlayModeReport.md`) |

The FBX files are kept outside `Assets/` so that Unity does not import the same meshes twice. The
scene uses the GLB exports, which have the same objects, names and pivots.

## Layout (Blender metres; Unity = (−x, z, −y))

**Ground floor**

* garage for 2 cars, with a roll-up door
* laundry, guest bathroom, guest bedroom
* foyer with the front door, study
* double-height living room with a 6.4 m glass wall onto the terrace
* open dining room and kitchen with an island and 3 stools
* glazed stair tower: a U-stair per storey (18 risers of 0.189 m, half landing), three storeys tall
* passenger lift beside the stair hall (see below)

**Upper floor**

* master bedroom opening onto the front balcony, with a walk-in wardrobe and master bathroom
* bedroom A
* family lounge with a TV
* shared bathroom
* gallery over the living room, and a side gallery
* bedroom B opening onto the rear balcony

**Third floor (z 6.8 m)**

* home cinema (3.6 m screen, two sofas)
* gym (treadmill, bench, mats, mirror wall)
* sky lounge with a bar and 3 stools
* powder room
* stair / lift landing
* **west roof terrace** (outdoor sofa, 3 loungers, planters, parasol), reached from the cinema and the gym
* **east roof terrace** (dining table for 4, parasol), reached from the sky lounge
* green roof with a skylight over the double-height living room

**Lift**

* Glass-back steel cab, about 1.5 × 1.5 m inside. It serves all 3 floors and really moves (accelerates, cruises, slows down).
* Sliding cab doors and landing doors. The landing doors only open at the floor where the cab is, and they stay open while someone stands in the doorway.
* Press E at the call button beside the doors. Inside the cab, press 1, 2 or 3.
* There is a floor indicator over each door, a chime on arrival and a hum while moving.
* The cab carries the player. The last floor it stopped at is saved.
* Residents take the stairs: NavMesh agents don't ride the lift.

**Outside**

* terrace
* 10 × 4.5 m pool: 1.1 m to 2.0 m deep, entry steps, deep-end ladder, underwater lights
* loungers with pastel parasols (Meshy Pastel Beach Umbrella), and an outdoor dining table
* planters, trees and garden lights
* driveway and front path
* perimeter wall, sliding gate, and the street with an exit to Metro City

## Interactions

**Doors**
* E opens or closes. L locks or unlocks the front door and the bathrooms.
* The garage door rolls up and the gate slides.
* Residents and the pet open doors for themselves.

**Seats**
* Every sofa, chair, stool, desk chair and lounger is a SeatController.
* Each one is linked to a resident ActivitySpot, so you can never sit where a family member is sitting.

**Fixtures (E)**
* Sleep: at night until 07:00, or a one-hour nap during the day. The screen fades to black.
* Computer
* Change clothes: recolours clothing materials if the avatar has separate ones, and says so if it has none.
* Fridge, cook, wash the dishes, wash your hands, laundry, snack
* TV on/off: the screen glows and changes colour, with a screen light.
* Water the plants
* Pet the cat

After using a fixture, press T to practise the related IELTS topic.

**Umbrella**
* A rain umbrella (Meshy Rainshadow, reduced from 500k to 15k triangles) waits in the stand by the front door.
* E takes it, E again puts it back. Carrying it is saved.
* Residents open one automatically when it rains and they are outdoors (garden, roof terraces, street).
* There is no umbrella-holding arm pose: it is held at the right hand, kept upright above the head.

**Car**
* The family car (Meshy Fidelity car) is in the garage.
* E gets you in. Drive out through the garage door and gate onto the street.
* Where the car was parked is saved.

**Pool**
* WaterBody plus the swimming system: swim, tread water, float, and climb out by the ladder.

**Lights**
* Room lights follow SkyState. Occupied rooms are at full brightness and empty rooms are dimmed.
* Garden and pool lights come on at night.
* `VillaState.Data.lightsMode` can force them `on` or `off`.

**Save**
* Saved to `villa_state.json` under persistentDataPath:
  * doors (open / locked) and the TV
  * car position
  * chores done today
  * outfit
  * topics completed

## Family (Living NPC system)

Schedules are editable in `Data/LivingNPC/Profiles/villa_*.asset`. Home is `villa`.

* **Michael (dad, architect):** commutes to `villa_city`.
* **Grace (mum, translator):** works at a home desk and walks in the garden.
* **Chloe (teen):** goes to school in `villa_city`.
* **Henry (grandpa):** garden, rest and TV.

"Away" means they leave through the gate and are simulated off-screen until they come back. The pet,
Mochi the cat, wanders between the garden, the sofa and the kitchen, and follows you after you pet her.

## Speaking: 8 home IELTS topics

Each topic has three steps: Part 1, then a Part 2 cue, then a Part 3 discussion. Every step has Band
3–4, 5–6 and 7–8 wording, a meaning pattern, a model answer and vocabulary.

**Topics:**
1. Your Home
2. Your Bedroom
3. Cooking and Food
4. Daily Routine
5. Free Time and TV
6. Studying and Working from Home
7. Pets
8. Gardens

**How a topic runs:**
* Topics run through `ScenarioManager`: answer, evaluate, correct, then repeat the corrected model. In Learning Mode the repeat cannot be skipped.
* Start a topic by talking to a family member. You get the topic that fits the room you are in, or the next one you haven't done.
* Or press T after using a fixture.

## V9 visual upgrade

See `VISUAL_QUALITY_REPORT.md` (inspection, changes, validation) and `DEVELOPMENT_PROGRESS.md`. Capture
before/after shots with ContextSpeak > Villa > Capture Visual Validation Shots (`Logs/VillaShots/`).
Surroundings: `LuxuryVilla_Surroundings.glb` (street, neighbours). Light switches: E by each room's door.

## Known limits / animations still needed

* **Lying in bed:** there is no lying-down clip, so the player sits on the edge of the bed (marked TEMPORARY) and residents use the existing NPC `Lie` pose.
* **Cooking, typing, washing and laundry:** these use the generic UseObject state. They need authored Cooking, Typing, Cleaning and Sleeping clips.
* **The cat:** it is a static Meshy mesh with no rig. It moves with a small trot bob and needs a rigged animal with walk, sit and idle clips.
* **Lighting:** it is realtime. Light probes and box-projected reflection probes are placed, but nothing is baked. Bake lighting for the final look (Window > Rendering > Lighting).
* **Entering from Metro City:** use ContextSpeak > Villa > Add Villa Entrance To Open Scene, and place the portal in the city.
