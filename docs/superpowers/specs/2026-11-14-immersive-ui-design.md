# ContextSpeak Life — Immersive, Realistic Game UI

Unity 6 · URP · UI Toolkit (keep using UI Toolkit; do not switch UI frameworks or use three.js)

You are a senior game UI/UX designer and Unity UI Toolkit engineer.
Redesign the in-game interface of ContextSpeak Life so it feels immersive and grounded in a
realistic modern life simulation (reference feel: The Last of Us Part II, Death Stranding,
Red Dead Redemption 2, Firewatch, inZOI). The player should look at the world, not at panels.

## 0. Inspect first
- Find every runtime UI: HUD (left info column, minimap, clock bar, controls bar), MAP panel,
  Journal (J), Bag (Tab), Settings (Esc), Friends, dialogue / speaking feedback / repeat panels,
  shop/menu/payment, toasts, interaction prompts.
- List their UXML/USS/C# files and which systems feed them. Preserve all existing gameplay,
  save data and the IELTS dialogue flow (answer → feedback → corrected sentence → repeat gate).

## 1. Design principles
1. Minimal HUD by default. Nothing permanent except a small, translucent clock/weather chip and
   the current objective (one line). Everything else appears only when relevant and fades out.
2. Diegetic and spatial first:
   - Map = a paper/phone map the character holds; zooms to the real minimap render of the area.
   - Journal, quests, messages, friends, wallet = an in-world smartphone UI (lock screen with time
     and weather, apps for Map, Messages, Wallet, Journal, Settings).
   - Menus, prices, opening hours = world-space signs/boards in the scene, readable at close range.
   - Interaction prompts = small world-space label near the object ("E · Order coffee"),
     never a big screen-centre box.
3. Contextual: energy/hunger/thirst only appear when they change or are low.
   Money appears when buying. Quest steps appear when one completes.
4. Realistic materials, not "game boxes": frosted glass (blurred background behind panels),
   paper texture for maps and receipts, subtle shadows, real typography. No default Unity grey
   widgets anywhere (dropdowns, scrollbars, toggles, sliders, text fields must be restyled).
5. Calm motion: 150–250 ms ease-out fades and slides; no bouncing; respect a "reduce motion" option.

## 2. Visual system (create one shared theme USS)
- Colour tokens: warm off-white #F7F3EA, ink #1F2A26, forest green #2E5E45, accent amber #C47A2C,
  error #B54A3A, success #3F7D52; panel glass = white 72% + 20px background blur.
- Type: one humanist sans for UI (e.g. Inter / Source Sans 3), one serif for paper items
  (e.g. Source Serif). Sizes 13 / 15 / 18 / 24 / 32. Never below 13 px at 1080p.
- Spacing scale 4 / 8 / 12 / 16 / 24 / 32; corner radius 10 (panels) / 8 (buttons) / 999 (chips).
- Contrast ≥ 4.5:1 for text; subtitles always readable over bright sky and snow.
- Restyle all controls: dropdown, scroller (thin, auto-hiding), toggle, slider, text field, tabs.

## 3. Screen-by-screen
- HUD: top-left clock chip (time · weather · day), top-right minimap (round, no overlapping labels;
  label collision avoidance), objective line under the clock. Remove the permanent controls bar —
  show a key hint only the first 3 times an action is available.
- Phone (Tab or M opens to the right app): Map / Quests / Messages / Wallet / Friends / Settings.
  Map app shows the real top-down map with location pins, route line and "Next stop".
  Split the current MAP list into tabs: Map · Speaking practice · Life scenes.
- Dialogue: cinematic letterbox (subtle), camera framing on the speaker, subtitles at the bottom
  with speaker name; the speaking feedback appears as a compact card beside the subtitle,
  then the corrected sentence card with a mic button for the repeat. Practice-feedback
  disclaimer stays visible; never show it as an official IELTS score.
- Shops/café: the menu is the board in the scene; ordering opens a receipt-style panel
  (paper texture), payment uses a card terminal / cash tray close-up.
- Toasts: small, top-centre, max 2 at once, queue the rest.

## 4. Technical requirements
- UI Toolkit runtime UI; one PanelSettings with scale-with-screen (1920×1080 reference),
  safe-area aware; supports 16:9, 16:10, 21:9 and 1280×720.
- Input: keyboard/mouse and gamepad navigation, visible focus states.
- Pause/slow world input while phone or dialogue is open; Esc always backs out one level.
- No per-frame allocations in UI updates; pool toast and prompt elements.
- Settings: UI scale, subtitle size, HUD on/off/minimal, reduce motion, colour-blind safe accents.

## 5. Deliverables
- Theme.uss (+ tokens), restyled controls, new Phone UI, new HUD, world-space prompt component,
  redesigned dialogue/feedback/repeat panels, migrated Map/Journal/Bag/Settings.
- Editor menu: ContextSpeak > UI > Capture UI Screenshots — renders each screen at 1920×1080 and
  1280×720 in day and night scenes into UIShots/.
- README section and DEVELOPMENT_PROGRESS.md: what is done, what is mocked, what is untested.

## 6. Acceptance checks
- No Unity default-grey widget remains (search USS for unstyled controls).
- No overlapping text in HUD/minimap at any supported resolution (capture and inspect).
- At rest the HUD covers less than 8% of the screen.
- Full IELTS flow still works: answer, feedback, corrected sentence, wrong repeat blocked,
  correct repeat advances.
- Do not claim it works without running Play mode and capturing screenshots.