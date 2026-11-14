# Downloaded vehicle and NPC application

Use FBXs supplied in `/Users/elaineting/Downloads/unitycomponent` through the existing optimized Unity library. The key animated protagonist/NPC archives and new Hummer match already imported hashes. Do not copy duplicate sources or revive retired static versions.

Apply the four unused standalone car models (Packard, 1930s sedan, 1957 Chevy, Hummer) to the existing four borrowable parking spaces in MetroCity. Preserve car roots, driving scripts, placement and collisions; normalize the new skins using the existing vehicle factory. Keep carport/rooftop diorama sources as existing static town scenery, not moving vehicles. Make CafeCharm the city barista and BraidedSchoolgirl the clothing adviser; retain Elliot as the friend and the two protagonist choices. Change visual children only, retaining NPC IDs, interaction objects, animations, navigation and saved progress.

Approach: an idempotent editor upgrade of the saved scene plus matching builder assignments. Rebuilding the whole city would overwrite the uploaded lighting and authoring edits; raw high-poly FBX placement would waste memory. Existing user delegation covers these routine casting choices. No commit, push or player export is requested.

Visual verification adjustment: the old mall parking strip overlapped the road. Relocate its four existing bays and markings to the mall east side (x=99, z=-94/-87/-80/-73), preserving driver roots and controls. Native checks must enforce clearance from roads/pavements.
