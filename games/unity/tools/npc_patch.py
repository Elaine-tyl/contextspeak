#!/usr/bin/env python3
"""Idempotent in-place patch: automatic doors also open for living NPCs. Run from the Unity project root."""
import sys
path = "Assets/ContextSpeak/Runtime/Interaction/DoorController.cs"
text = open(path, encoding="utf-8").read()
marker = "LivingNPC.All"
anchor = "            foreach (var npc in NPCController.All)\n"
if marker in text:
    print(path + ": 0 edit(s)")
else:
    if anchor not in text:
        print(path + ": anchor not found"); sys.exit(1)
    text = text.replace(anchor,
        "            foreach (var living in LivingNPC.All)\n"
        "                if (living != null && (living.transform.position - transform.position).sqrMagnitude < 2.5f * 2.5f) return true;\n" + anchor, 1)
    open(path, "w", encoding="utf-8").write(text)
    print(path + ": 1 edit(s)")
