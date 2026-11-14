# Uploaded Unity design integration

Use the user's ContextSpeak-changes-art.zip and ContextSpeak-changes-code.zip as the visual baseline for the existing Unity project. Inspection found all 81 art files and 119 code/document files already identical. Preserve the later compatible changes in CafeBuild, MotionAssets and MeshyCharacters: ordinary story scenes must ship, all supplied animation takes must remain available, and character facing must stay corrected. Do not replace these files with older archive versions.

Verify the existing nine environment-enabled scenes and inspect rendered output. The uploaded greenhouse design explicitly intends the authored glass café to replace the imported shell. Its current renderer-only suppression allows LOD/room visibility to reactivate that shell. Add a serialized interior-only mode to BuildingSkin, apply it through the greenhouse upgrade, and verify ordinary room behavior remains unchanged. Preserve gameplay objects, speech, saved progress, authoring scenes and GUIDs. No commit/push or player export is requested.

Approach: reconcile existing files by content and apply only the missing scene/compatibility fix. Full archive replacement would regress later work; duplicating imported art would waste space and change references.
