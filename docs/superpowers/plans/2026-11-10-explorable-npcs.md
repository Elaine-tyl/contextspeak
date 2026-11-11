# Explorable NPC Areas Implementation Plan

> **For agentic workers:** Use test-driven development. World/layout and NPC catalog tasks may run independently; root owns controller, UI and integration.

**Goal:** Walk freely through connected areas, meet different NPCs and complete speaking tasks at their locations.

**Architecture:** Keep the existing three-goal lesson and local speech pipeline. Each authored turn has an NPC identity whose ID maps to a physical zone. World layout and safe navigation are separate data. Entering a scene starts exploration; only a nearby NPC and an explicit E/Talk action open dialogue. A successful reply finishes that conversation and points to the next stop.

**Tech Stack:** Godot 4.7.2, TypeScript/Zod, local Qwen/Whisper/Phonon, Vitest and Playwright.

## Design decisions

- All five roleplay settings get three connected areas and three named actors. The original cafe mission stays compatible.
- Primary movement is WASD/arrows with a following camera. The optional Space/Walk helper moves along safe corridors without teleporting or opening a conversation automatically.
- Scene entry and resumed roles start at an entrance. Task progress and pending corrections remain saved per role; position is not a new learning score.
- Nearness determines which NPC E addresses. Current quest actors ask their authored question; future/completed actors give voiced greetings and direction. Free exploration is unrestricted while learning goals advance in sequence.
- Passing a goal plays that actor's acknowledgement, closes the panel and highlights the next NPC. Corrections stay with the same actor until readback succeeds. Voice failure must not trap the player in a finished dialogue.
- Questions retain casual wording. Provider context uses per-turn `npc:{id,name,role}`, not the role's first NPC.
- World API adds `nearest_npc_id`, `npc_data`, `set_target_npc`, `focus_npc`, `current_zone` and optional NPC ID in `walk_to_npc`. `configure` accepts an optional role ID. Existing action effects and reduced motion remain intact.
- All UI/code/docs English. No new external assets, dependencies, commit or push.

## Tasks

- [x] Catalog/backend: test and add strict per-turn NPC identity; validate three distinct zone routes and use the current speaker in AI context.
- [x] World: test/build 36×28m connected layouts, three actors, safe paths, collisions, following/dialogue cameras, proximity targeting and manual steering.
- [x] Controller/UI: failing tests for exploration-first entry, nearby-only activation, locked greetings, correction locality, next-stop transitions, cancellation and saved progress; implement the exploration/dialogue lifecycle.
- [x] Browser: exercise free walking, physical arrivals before E, multiple NPC questions, recorded readback, next-stop transitions and switching/reload failures.
- [x] Verify TypeScript suite/typecheck/format, all Godot scripts, Web/macOS export, native startup, real local current-speaker request and browser screenshots; independent review and fixes.

## Review focus

An E press far away cannot start a remote conversation. Passing a reply cannot ask the next NPC's question from the previous NPC. Locked greetings cannot submit or award goals. Voice/cancel/switch races cannot leave the player frozen or alter another scene. Every task actor must be physically reachable and the helper must move rather than teleport. Pending corrections restore at the correct actor and existing cafe controls still work.
