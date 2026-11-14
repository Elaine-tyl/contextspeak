# Immersive Unity UI Implementation Plan

**Goal:** Replace panel-heavy game overlays with a minimal contextual HUD, phone navigation and cinematic speaking UI while preserving all gameplay/save gates.
**Architecture:** Shared UI Toolkit PanelSettings/theme/preferences. Existing C# game state remains authoritative; new visual surfaces issue existing commands. Legacy location UI is audited and migrated without changing scenario/economy logic. Saved scenes are never regenerated.
**Spec:** `docs/superpowers/specs/2026-11-14-immersive-ui-design.md` (user-supplied design; existing authorization delegates remaining design choices).
**Constraints:** Unity 6000.3.28f1/URP/UI Toolkit; no three.js; no new cloud services; no scene overwrite; no implied commit/push. Actual Play-mode screenshots before claiming acceptance.

## Runtime inventory / feeds
- `_Game/UI/CafeInterface.cs`, `LifeInterface.cs`: CafeGame/LifeGame snapshots; CafeStyles.uss, shared document factory.
- `_Game/Slice/Runtime/SliceInterface.cs`: SliceGame/SliceView, SpeakingCoach repeat gate, cafe cart/payment, quest/needs, RoomClient, map markers; SliceStyles.uss.
- `_Game/LivingWorld/Runtime/LivingWorldInterface.cs`: DynamicLifeEventManager, branching story/repeat gate; LivingWorld/Styles.uss.
- `ContextSpeak/Runtime/UI/LifeUI.cs`: legacy IMGUI HUD, ScenarioManager/SpeechSession/SaveManager.
- `ContextSpeak/Runtime/Commerce`: legacy IMGUI menu/cart/payment.
- `ContextSpeak/Runtime/Airport/*Flow*.cs`, `AirportUI.cs`: airport/cabin/arrival flow IMGUI.
- `ContextSpeak/Runtime/Locations/*Flow.cs`, Beach/BeachFlow.cs, Zoo/ZooFlow.cs: location-specific IMGUI activity panels.
- EnvironmentDebugPanel, SkyHUD, swimming/craft labels: additional world/debug overlays requiring suppression or integration.

## Tasks
- [x] Shared foundation: Theme.uss tokens/controls, shared 1920x1080 PanelSettings and safe area, persistent UI accessibility preferences, pooled toasts/prompts. Tests for settings, timing and bounds.
- [x] Slice redesign: contextual <8% HUD, real top-down map, phone apps/tabs/back navigation, pooled markers, contextual world-space prompts, compact subtitles/feedback/repeat, receipt order UI. Preserve commands and input gates. Regression checks first.
- [x] Legacy/native surface migration: remove visible IMGUI panels; toolkit replacements for life/location/commerce UI. Keep existing state machines and compatibility methods while clearly documenting any unmigrated surfaces.
- [x] Auxiliary UI and acceptance harness: style old Cafe/Life/Story UI, screenshot menu at day/night + 1920x1080/1280x720, native layout/repeat checks, README/progress audit.
- [x] Integrate, native compile/tests, Play-mode capture and inspect supported aspect ratios, exercise wrong/correct repeat and back navigation. Export browser only after green source checks; retain prior working build.

## Integration contracts
- Shared namespace `ContextSpeak.Cafe.UI`: `GameUiRoot.Attach(GameObject)` -> VisualElement; `UiPreferences.Current`, `Changed`, `AddControls(VisualElement)`; settings `Scale`, `SubtitleSize`, `Hud`, `ReduceMotion`, `ColourBlind`.
- Existing `CafeInterface.CreateRoot` will call the shared factory (root owner handles this one edit). Consumers add their own sheet after Theme.uss; shared classes are `phone`, `phone-header`, `phone-apps`, `subtitle-card`, `practice-note`, `paper`, `receipt`, `ui-chip`.
- Source and generated scene files stay untouched. Tests/build use an isolated project copy if the user's authoring Editor is busy.

## Review focus
- A wrong spoken repeat cannot advance; typed rehearsal cannot satisfy spoken gates.
- Esc closes the current child panel before the phone/dialogue, and gameplay movement stays blocked during overlays.
- No invisible full-screen element intercepts world input; shared PanelSettings and scene reload do not leak documents/textures.
- Long translated lines and gamepad focus remain usable at 1280x720 / ultrawide / increased UI scale.
- Minimal HUD stays under 8%; context prompts are near their actual world anchor and do not overlap the minimap.

## Verification ledger
- Shared foundation native checks: passed; bundled Chinese glyph and flat paper texture assertions included.
- Legacy native control/modal checks: passed; Menu/Payment/Airport→Settings isolation and one-level Back covered.
- Focused real SliceGame transactions: passed; typing/no credit, wrong/correct repeat, order/payment idempotency and suspended guided route covered. Synthetic input; no provider request.
- Pure UI policy: 15 slice assertions +18 foundation assertions passed. Existing slice core335 assertions passed. Python source contracts7 and browser bridge/pointer-lock5 passed.
- Native Play-mode capture: 80 exact-size day/night images, acceptance.json has no failures; resting HUD3.9–4.1%. Representative screens inspected after repairing offscreen subtitles, white phone text, missing paper import, native slider skin and story/minimap overlap.
- Browser export20261113.220636 completed and published locally; all7 exported scene hashes match authoring files. Browser reload/runtime verification is recorded separately in docs/IMMERSIVE_UNITY_UI.md.
- Independent review defects fixed: progress backup key, modal ownership, duplicated DOM overlays, CJK fallback, route suspension.

## Remaining presentation work / explicit limits
- [ ] Authored handheld phone prop and hand animation; world menu boards and payment-terminal close-up.
- [ ] Full legacy location visual pass, hardware gamepad, fresh human recording and new macOS player QA.
- [ ] Largest UI-scale combinations and actual target-device GC/GPU profiling. No zero-allocation claim.

- Final Chrome verification: build20261113.220636; Map and Escape navigation exercised; Bootstrap/additive story launcher overlap fixed and rechecked. No fresh human microphone or macOS package claim.
