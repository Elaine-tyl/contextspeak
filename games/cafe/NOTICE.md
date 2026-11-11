# Third-party components

The café room layout, dialogue and current procedural campus appearance are original. Quaternius CC0 characters, KayKit Restaurant Bits 1.0 FREE furniture,
and selected Kenney CC0 food/decor are vendored with provenance in assets/CREDITS.md and
assets/sources.json. No FluentRead code or paid game assets are used.

Runtime/build dependencies are downloaded separately into ignored `.local/`:

- Godot Engine and export templates: https://github.com/godotengine/godot — MIT.
  Godot's bundled third-party notices remain in its distributions.
- whisper.cpp: https://github.com/ggml-org/whisper.cpp — MIT.
- Whisper base.en model: https://huggingface.co/ggerganov/whisper.cpp — see model
  card and upstream Whisper MIT notices.
- Qwen3 4B: https://ollama.com/library/qwen3 — see the pinned model's Apache-2.0
  license and upstream model card.
- Phonon 31M English: https://ollama.com/library/phonon — Apache-2.0. This project
  pins 31M English; the 40M checkpoints have different, noncommercial terms.
- CMake: https://cmake.org/licensing/ — BSD-style license.

This prototype does not package or redistribute model weights. Before a public
binary release, include the required engine/dependency notices and review the
exact model licenses and redistribution conditions. Free local inference still
requires suitable hardware and storage.

Godot Engine is used under the MIT license. Copyright (c) 2014-present Godot
Engine contributors; copyright (c) 2007-2014 Juan Linietsky, Ariel Manzur.
Engine license and notices: https://godotengine.org/license/.
