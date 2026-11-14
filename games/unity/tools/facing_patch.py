"""Idempotent patch: CozyCharacters.DressPlayer must parent the player visuals with identity local rotation.
Without it, a player spawned with a body yaw != 0 walks backwards (visual keeps -yaw). Run from games/unity."""
import sys, os
p = os.path.join(sys.argv[1] if len(sys.argv) > 1 else ".", "Assets/Editor/Cozy/CozyCharacters.cs")
s = open(p).read()
if "appearance.Girl.transform.localRotation = Quaternion.identity" in s:
    print("facing_patch: already applied"); sys.exit(0)
a = "            appearance.Boy.transform.SetParent(avatar, true);\n"
assert a in s, "anchor not found"
s = s.replace(a, a + "            // Visuals face the Avatar's +Z; never inherit the spawn yaw (that made the player walk backwards).\n"
                     "            appearance.Girl.transform.localRotation = Quaternion.identity;\n"
                     "            appearance.Boy.transform.localRotation = Quaternion.identity;\n")
open(p, "w").write(s); print("facing_patch: applied")
