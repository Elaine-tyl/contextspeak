# Character creation and Life prototype status

This update adapts the supplied `ContextSpeak_CharacterCreation_Unity6.zip` into the existing Unity project. It does not install its primitive avatars, legacy input module or separate build configuration.

## Play

Run the local companion with `pnpm world:dev`, then open `http://127.0.0.1:4320/unity/?world=slice` in Chrome. New installations see character creation first. Choose either supplied protagonist, enter a name, choose an initial practice Band (3–9), adjust height/build within ±5%, rotate the actual model, and save to enter.

At **Settings → Edit name and appearance**, or at the **home wardrobe / mall fitting room** with E, reopen creation. Returning without changes discards the draft. Saving returns to the same world position with existing money, orders, tasks and difficulty intact. Starting Band initializes new world saves; it does not reset an established learning target. The map includes a wardrobe shortcut.

The classroom is on the northwest road. Use its timetable with E, then talk to Ms Taylor with F. Her original three-turn task adapts to the current Band 3–9 practice target and uses the same recorded-answer/correction/retry pipeline as the other residents. Completed conversations appear in the learning journal. These targets and feedback are not official IELTS scores.

## Supported appearance

| Feature | State |
| --- | --- |
| Male Daydream / female Academy | Implemented with the actual supplied Meshy skins; both usable in all three worlds |
| Character name, initial Band, local persistence | Implemented; versioned JSON preference separate from mission saves |
| 360° preview, height and build | Implemented; bounded to ±5%, with matching player capsule dimensions |
| Wardrobe / fitting preview, cancel/save/return | Implemented; draft edits do not mutate the saved profile |
| Face shape, independent skin/eye/hair changes | Pending compatible blend shapes, masks or separate model parts |
| Hairstyles, purchasable outfits/shoes/backpacks | Pending modular wardrobe meshes and an item/economy integration |
| Supplied default hair/eyes/uniform/backpack | Preserved; there are no misleading controls for unsupported changes |
| Natural full-body clips, facial expressions, English lip sync | Partial procedural motion only; dedicated animations/facial rig still required |

Appearance JSON is stored under the local Unity PlayerPrefs key `contextspeak.character-profile`; the earlier gender-only preference remains compatible. Corrupt profiles reopen creation. Unsupported newer profile versions are protected from overwrite. No account, API key or recording is stored in the profile.

## World feature matrix

| Area/system | Current playable prototype | Still to implement |
| --- | --- | --- |
| Home | Walkable interior, wardrobe, conversation; existing Life town family grocery errand | Bedrooms/bathroom/garage, cooking, TV, computer, pets and family gatherings |
| School | Reachable classroom, timetable, teacher, Band 3–9 task and saved completion | Full campus, library borrowing, classes/seat interaction and dedicated timed Speaking Parts 1–3 |
| Forest café | Spoken ordering, item pictures/options/cart, payment, preparation/delivery, seats, consumption | More service variations and authored performance animations |
| Restaurant | Interior and adaptive conversation | Reservations and a separate restaurant transaction/meal service loop |
| Market | Interior/conversation; existing Life town groceries, wallet and bag | General supermarket shelves, shopping trolley and self-checkout |
| Mall | Walkable interior, assistant and real character fitting preview | Buying modular clothing, refunds, salon and actual outfit inventory |
| Fishing | Instructor, cast/reel/catch loop, XP and saved catch log | Tackle purchases, tournaments, selling/cooking fish |
| Amusement park / Japan kitchen | Prototype environment and contextual dialogue | Ride controls, admission, queue simulation, travel access and restaurant transactions |
| Farm, pool/gym, apartment | Not implemented | Add one complete interaction/economy loop at a time |
| Hospital, bank, police, court, office | Not implemented | Country-specific authored communication simulations and contextual forms |
| Airport / country travel | Not implemented | Separate destination scenes, check-in/border/hotel/transport tasks |
| Time/weather, wallet/bag, journal and saves | Existing connected prototype systems retained | Work income, relationships, daily schedules and full farming-to-shopping economy |
| Speech | Existing STT, NPC voice, semantic feedback and correction gating retained | Dedicated pronunciation service for real audio-based pronunciation metrics; no fabricated scores |

The remaining phases are production work, not hidden behind placeholder scenes or described as complete. No new database, RAG service or paid provider is added here.

## Validation

Use `pnpm unity:test` for profile, source geometry, scene, navigation, transaction, native UI and runtime interaction checks. Use `pnpm test:unity` for the browser flows; tests complete first-run onboarding rather than bypassing it. `games/unity/tests/character.spec.ts` covers first-run setup, rotating/scaling both models, wardrobe cancel/save/reload, fitting-room entry and mobile controls. The shared resident test covers the classroom's recorded correction before progression.

Build with `pnpm unity:build:web` and `pnpm unity:build:mac`. Generated scenes live under `games/unity/Assets/Generated`; source code is under `Assets/_Game/CharacterCreation` and Editor builders. Both builds continue using the pinned Unity 6000.3.28f1 LTS.

The regression suite also holds startup/reconnect requests to verify that changing practice stages cannot cancel the local AI connection, and a late old-session failure cannot disconnect a new session. An additive-scene handoff test prevents an outgoing world snapshot from hiding the creator. These checks use fixture speech/transcription/review responses; they verify the recording and correction workflow, not a human pronunciation score.

Verified UI captures: [desktop creator](images/character-creation.png), [male protagonist](images/character-creation-male.png), [phone layout](images/character-creation-mobile.png), [classroom task](images/character-classroom.png).

Latest verification: 609 project tests, 12 asset-pipeline tests, native Unity profile/scene/navigation/UI/runtime checks, 26 browser flows, four packaged creator smoke tests and TypeScript checking passed. Web and macOS builds completed. Actual desktop first-run creation was visually checked; no new human microphone/pronunciation assessment was performed in this update.
