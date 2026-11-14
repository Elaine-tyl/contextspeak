# Player motion integration

The current Unity source uses the supplied `unitycomponent/女主.zip`, not a replacement animation pack. Its Academy animation FBX matches `Assets/_Game/Art/Motion/Academy-All.fbx` (SHA-256 `baa95b91fb12f1d10aa49b658116b444d7d7751f7abb940d2dedea7b7cfb4b71`).

## Uploaded design

- All 81 files from `ContextSpeak-changes-art.zip` match the project.
- All 122 files from `ContextSpeak-changes-code.zip` are present. Seven retain later fixes: environment validation, greenhouse shell visibility, vehicle/NPC selection, build configuration, animation import and character prefabs. Reapplying the older ZIP wholesale would remove those fixes.
- This repair changes animation code and generated character prefabs; it does not regenerate the saved environments.

## Motion ownership

`CafePlayer` moves the CharacterController. WASD is relative to the camera; the visual turns toward current momentum, including when reversing direction. The imported skeleton plays in place without procedural body scaling or bobbing.

Walking and running use their corresponding supplied clips. Jumping uses the longest airborne section of Academy's `Run_and_Leap`, with the original run-up removed. Hip translation is held at the canonical standing height so gravity is applied once. The ascent, apex and descent follow the physics jump phase, including long falls. All original takes remain available for preview.

A static grounded squat/landing pose supplies crouching where the character pack supports it. Sitting retains its rigid seat offset. Character height/build customization stays on the outer appearance root.

## Reproduce

1. Open `games/unity` with the pinned Unity version.
2. Run **ContextSpeak → Meshy → Apply complete character animations** after changing supplied FBX data or the animation builder.
3. Run **ContextSpeak → Checks → Player locomotion**. It checks walk/run facing in four directions, reversal, deformation, seating, crouch bounds, jump phase/root ownership and return to idle. A failure throws and writes `.local/locomotion/checks.json`.
4. Run **ContextSpeak → Export current design → Web browser** to export saved scenes without rebuilding their layout.
5. Reload the browser game and confirm the visible build ID changed. Check WASD, Shift, Space, C, sitting and an NPC conversation.

The wider V6 vehicle, swimming and world-physics specification is separate from this locomotion repair. Browser and desktop exports must each be rebuilt before claiming both contain a source change.
