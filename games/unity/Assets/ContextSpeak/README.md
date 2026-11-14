# ContextSpeak Life — three interactive locations (Unity 6)

快速开始 / Quick start
1. Unity 菜单 **ContextSpeak > Build Life Simulation Locations**（生成 Hospital.unity、Restaurant.unity，升级 Forest Café，并加入 Build Settings）。
2. 再运行 **ContextSpeak > Build Metro City (city life)**，城市里的医院、餐厅门口和中央车站会出现入口（绿色门垫）。
3. 打开 `Assets/Generated/MetroCity.unity` 按 Play，走到医院/餐厅门口按 **E** 进入；在车站按 **E** 坐火车去 Forest Café，在森林车站按 **B** 回城。
4. 免费本地语音：在 ContextSpeak 文件夹运行 `pnpm world:dev`。没有运行时会自动进入标明的 **MOCK MODE**（打字代替说话）。

## Controls (hospital & restaurant)
| Key | Action |
|---|---|
| WASD / Shift / Space | move / run / jump |
| E | talk, sit, stand, eat/drink, doors to other scenes |
| R | start/stop recording your answer |
| H / J | hear the NPC (or the sentence to repeat) again / slowly |
| N | skip a line (free-practice mode only) |
| Esc | leave the conversation |
| I | inventory + quests |
| F1 | settings: IELTS band 3–9, learning mode, force MOCK mode, reconnect, reset |

## How a conversation works
Each NPC line exists in three versions (Band 3–4 simple, 5–6 standard, 7–9 advanced). Your answer is
judged on **meaning** (did you do the task?) by the free rule-based `SpeakingCoach` — no paid AI.
* Wrong task → a hint; after 3 tries (learning mode) a model answer you must say aloud.
* Right meaning but a correction → the **repeat gate**: say the corrected sentence before the dialogue moves on.
  A different or wrong sentence never advances.
* Learning mode off = free practice: no forced repetition, `N` skips.

## Locations
**City Hospital (45 × 35 m)** — A entrance & reception · B waiting area + queue board · C consultation rooms ·
D pharmacy · E nurse station, admin office, lifts, upper gallery. Flow: receptionist → registration form →
appointment → queue ticket → sit and wait → number called → nurse → practice case card → doctor →
consultation summary → pharmacist → medicine label → payment → leave. All medical content is fictional.

**Harbour Table (35 × 25 m)** — host stand, window / inside / terrace tables (one reserved), open kitchen,
beverage station, takeaway counter. Flow: host (reservation, party size, preference) → choose table → sit →
drinks → menu (categories, descriptions, allergens, quantities, customisations, cart) → say the order →
allergy check → kitchen timer → waiter serves → plates appear → eat/drink → "how was everything?" →
bill (10 % tip, split) → pay. Optional: complaint/refund with the manager, takeaway at the counter.

**Forest Café** — the existing café (menu, customisation, waiter, payment, seats, eating) upgraded with a
stream, waterfall + mist, ferns and moss rocks, a timber-and-glass terrace roof, forest visitors, a train stop
to Metro City, and served orders → inventory + life XP.

## Editing content
* Conversations: `Assets/ContextSpeak/Data/Scenarios/*.asset` (lines per band, goal, meaning pattern, model, vocabulary).
* Menus: `Assets/ContextSpeak/Data/Menus/*.asset` (prices in cents, allergens, options `Group:Choice/Choice+cents`).
* Rebuilding keeps your edited assets; delete one to restore the built-in version.

## Code map
`Runtime/Core` save, money, inventory, quests, scene transitions · `Runtime/Dialogue` scenarios, state machine,
speech · `Runtime/Interaction` interactables, doors, seats, NPCs, portals, food · `Runtime/Commerce` menu, cart,
payment · `Runtime/Locations` hospital, restaurant, café flows · `Runtime/UI/LifeUI` HUD ·
`Editor/` builders and `LifeLocationChecks` (menu **ContextSpeak > Checks > Life Locations**).

## Sky & weather (URP)
The project uses **URP** (Forward, HDR), so the sky is URP-native — no HDRP features.
* `ContextSpeak/Dynamic Sky` skybox shader: gradient sky driven by the sun's elevation, Mie glow and sunset band,
  two drifting fbm cloud layers with self-shadowing and silver lining, phased moon, twinkling stars, optional HDRI blend.
* `SkySystem`: time of day (sunrise 06:12, sunset 18:36 by default), sun + moon lights, cloud shadows (scrolling
  light cookie), trilight ambient, fog, exposure, sky-only realtime reflection probe, rain, wetness, smooth weather blends.
* Weather: Clear, Cloudy, Rain, Overcast (editable `Assets/ContextSpeak/Sky/Weather-*.asset`), seven presets:
  clear morning, bright afternoon, golden sunset, cloudy, rainy, clear night, overcast night.
* Gameplay: street lamps and headlights switch on at dusk / in gloom, interior lights and windows glow at night,
  fewer pedestrians at night and in rain, walking NPCs shelter, terrace seats close in rain (restaurant and Forest
  Café), birds roost, water and roads change with weather, procedural rain/wind/cricket/thunder sound.
  Fishing already depends on the Forest Café clock's weather and dawn/dusk, which the sky now follows and shows.
* Settings: on the `Sky & Weather` object (day length, time, sunrise/sunset, clouds override, wind, weather, night on/off)
  and in game with **F2**.
* Menus: **ContextSpeak > Sky & Weather > Install in All Life Scenes**, **Render Test Shots**, **Checks > Sky & Weather**.
* Photographic skies: import a CC0 `.hdr` (e.g. Poly Haven) as a Cubemap and drop it into HdriDay/HdriSunset/HdriNight.

## International airport (ContextSpeak > Build International Airport)
Scenes: `Airport.unity` (exterior, departure hall, check-in, baggage, security, passport control, shops,
three gates, airfield), `AircraftCabin.unity`, `ArrivalAirport.unity` (+ destination hotel lobby).
Getting there: rebuild Metro City, then at **Central Station** use the right-hand mat — "Airport Express".
Quest **AIRPORT_TRAVEL_001 · First International Trip** (17 objectives, strictly in order, saved).

| Where | What you do |
|---|---|
| Station / forecourt | arrive by Airport Express (taxi driver to chat with at the drop-off) |
| Departure hall | book a flight at a kiosk (KL, London, Melbourne, Tokyo, New York; purpose; days), read the departures board |
| Check-in A3 | talk to the agent (passport, destination, seat, bags) → seat map → scale, excess fees / repack → baggage questions → tags → boarding pass |
| Security | officer → tray mini-game (fictional rules) → X-ray + arch → collect items |
| Passport control | officer checks destination, purpose and stay **against your booking** |
| Concourse | duty free, books, souvenirs, QuickStop, Skyline Coffee, Runway Kitchen (sit, order, eat), currency exchange, lounge |
| Gate | gate info, boarding announcement, gate agent validates the boarding pass (wrong gate / too early / no pass…) → jet bridge |
| Cabin | stow bag, find your seat, B = seatbelt, take-off, flight attendant (drinks, meal, blanket), in-flight map, landing |
| Arrival | immigration (purpose, stay, hotel), carousel 2 (or report a missing bag), customs, tourist info, taxi booking → hotel check-in |

Keys: M map · E use/talk · R speak · H/J hear/slowly · B seatbelt · I inventory · F1 settings · F2 sky.
All documents, fees, rules and announcements are fictional practice, not official travel or border guidance.
