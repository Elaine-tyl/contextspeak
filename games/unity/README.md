# ContextSpeak Unity

Unity6000.3.28f1 LTS runs the Life world and original 3D cafe. The active 2D
learning games use Godot in `games/arcade`; the earlier Unity arcade experiment
is retained as historical source. Follow [engine setup and controls](../../docs/GAME-ENGINES.md).

Runtime code lives in `Assets/_Game`: Core, Player, NPC, AI, Data, World, UI and
Arcade. Editor builders generate scenes, capability/quest assets, navigation,
Addressables bundles and free-art imports. Art provenance remains under
`games/cafe/assets`; it is shared source material, not a Godot runtime dependency.

Preserve mission save keys and the host-owned arcade scoring boundary when
adding districts or activities. Do not put AI keys, account tokens or learner
recordings into this project or its exported assets.
