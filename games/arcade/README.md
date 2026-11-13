# ContextSpeak Godot 2D games

The active two-dimensional game board uses Godot 4.7.2 and GDScript. All twelve
listening/speaking modes reuse the website's accounts, ten levels and scoring.
Unity owns the separate 3D Life world and cafe.

```sh
pnpm arcade:setup
pnpm arcade:test
pnpm arcade:build
pnpm dev
```

Open `/games`; `/games/arcade` is an alias. `/games/web` keeps accessible HTML
controls and can resume the same saved run. The host owns audio, recording and
assessment; the iframe never receives credentials or microphone bytes.

`board.gd` implements native controls and the card layout; `picture.gd` draws
the hero banner and original 2D pictures; `ui_art.gd` draws icons, the score
ring and level dots. Missing-word sentences type themselves out and each blank
pops in; keystrokes bounce the field. The palette matches the website: green
for actions, blue for listening, orange for levels and progress.
Generated exports and caches are ignored. See [engine setup](../../docs/GAME-ENGINES.md).
