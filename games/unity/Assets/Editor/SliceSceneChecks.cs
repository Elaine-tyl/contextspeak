using System;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Editor
{
    /// Scene-level checks for the generated Forest Café slice (run in the Editor / batch mode).
    public static class SliceSceneChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Forest Café scene: " + message);
        }

        private static bool SupportsUrp(Material material)
        {
            // Batch tests have no active GPU subshader; inspect serialized pipeline tags instead.
            var shader = ShaderUtil.GetShaderData(material.shader);
            return Enumerable.Range(0, material.shader.subshaderCount).Any(index =>
                shader.GetSerializedSubshader(index).FindTagValue(new ShaderTagId("RenderPipeline")).name == "UniversalPipeline");
        }

        [UnityEditor.MenuItem("ContextSpeak/Rebuild Forest Café slice only")]
        public static void RebuildSliceOnly()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SliceScene.Create();
            EditorSceneManager.SaveScene(scene, "Assets/Generated/ForestSlice.unity");
            VerifyScene();
            Debug.Log("Forest Café slice rebuilt and verified.");
        }

        public static void VerifyScene()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<SliceGame>();
            Check(game != null && game.Player != null && game.Rowan != null && game.Hazel != null && game.Counter != null, "runtime references are wired.");
            var residents = UnityEngine.Object.FindObjectsByType<LearningNpc>(FindObjectsSortMode.None);
            Check(LifeCurriculum.Places.All(place => residents.Any(npc => npc.SceneId == place.Id)), "every reference-led place has an interactive learning NPC.");
            Check(game.Player.Model.GetComponentInChildren<AuthoredMotion>() != null, "the player uses the editable reference-led character rig.");
            var playerSkin = game.Player.Model.GetComponentInChildren<SkinnedMeshRenderer>();
            Check(playerSkin != null && playerSkin.bones.Length >= 50, "the player has a deforming anatomical mesh rather than separate block limbs.");
            Check(AssetDatabase.GetAssetPath(playerSkin.sharedMesh).EndsWith("Motion/Academy-Vault.fbx"), "the supplied rigged Meshy player is used in the scene.");
            Check(playerSkin.bounds.size.y > 1.5f && playerSkin.bounds.size.y < 2.1f, "the skinned player culling bounds allow modest animation padding.");
            Check(UnityEngine.Object.FindObjectsByType<CafeCustomer>(FindObjectsSortMode.None)
                .All(customer => customer.GetComponentInChildren<SkinnedMeshRenderer>(true)?.bones.Length >= 7), "ambient customers have their own generated locomotion skeletons.");
            Check(playerSkin.sharedMaterials.All(SupportsUrp), "imported player materials support URP: "
                + string.Join(", ", playerSkin.sharedMaterials.Select(material => material.name + "=" + material.shader.name)));
            var lodge = GameObject.Find("Speaking lodge");
            Check(lodge != null && lodge.GetComponentsInChildren<MeshRenderer>().Length > 8, "the original Blender lodge is imported as real 3D meshes.");
            Check(UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).Any(item => item.Id == "speaking-board"), "the lodge has a usable speaking mission board.");
            var guide = residents.First(npc => npc.SceneId == "home");
            var barista = residents.First(npc => npc.SceneId == "cafe").GetComponentInChildren<MeshFilter>();
            Check(barista != null && AssetDatabase.GetAssetPath(barista.sharedMesh).EndsWith("Meshy/MaidHall.glb"), "the café learning NPC uses a separate adult Meshy character.");
            Check(guide.GetComponentsInChildren<MeshFilter>().Any(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).EndsWith("Meshy/Elliot.glb")), "the friend uses the supplied Meshy character as actual geometry.");
            Check(game.Waiter != null && game.Waiter.Counter != null && game.Waiter.Pass != null && game.Waiter.Tray != null, "the waiter can collect and deliver orders.");
            Check(game.Seats.Length >= 14 && game.Seats.Count(seat => seat.Table == "T1") == 2 && game.Seats.All(seat => seat.TableTop != null), "indoor, terrace and picnic seats exist with table tops.");
            Check(game.Pier != null && game.Pier.Stand != null && game.Pier.WaterTarget != null && game.Pier.Rod != null, "the fishing pier is set up.");
            var fishingMaterial = new SerializedObject(game.Pier).FindProperty("LineMaterial");
            Check(fishingMaterial != null && fishingMaterial.objectReferenceValue != null, "fishing material is serialized so player builds keep its shader.");
            WorldFrameworkChecks.VerifyNavigation("ForestSlice");
            Check(game.Atmosphere != null && game.Atmosphere.Rain != null && game.Atmosphere.Birds.Length > 0, "weather and wildlife exist.");
            Check(game.DrinkProps.Length > 0 && game.FoodProps.Length > 0 && game.PlayerHand != null, "delivery props and the hand anchor exist.");
            var player = game.Player.GetComponent<CharacterController>();
            Check(Mathf.Approximately(game.Player.WalkSpeed, 4.5f) && Mathf.Approximately(game.Player.RunSpeed, 7f) && Mathf.Approximately(game.Player.SprintSpeed, 9f), "movement speeds match the spec (4.5 / 7 / 9 m/s).");
            Check(player.stepOffset >= 0.3f && player.slopeLimit >= 40, "the controller climbs steps and slopes.");
            Check(game.Player.GetComponent<FootstepAudio>() != null, "footsteps are audible.");
            Check(UnityEngine.Object.FindObjectsByType<CafeCustomer>(FindObjectsSortMode.None).Length >= 4, "the café has ambient customers.");
            Check(UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None).Length >= 40, "forest trees use LOD groups.");
            Physics.SyncTransforms();
            Check(Physics.Linecast(new Vector3(0, 0.8f, 13.2f), new Vector3(0, 0.8f, 14.6f)), "the pond edge stops the player walking into the water.");
            Check(!Physics.Linecast(new Vector3(5, 0.8f, 13.2f), new Vector3(5, 0.8f, 17)), "the pier is open to walk onto.");
            Check(Physics.Linecast(new Vector3(5, 0.8f, 17), new Vector3(5, 0.8f, 19)), "the end of the pier has a rail.");
            Check(Physics.Raycast(new Vector3(-6, 3, 20), Vector3.down, out var bridge, 5) && bridge.point.y > 0.5f, "the bridge deck is walkable above the water.");
            Check(Physics.Linecast(new Vector3(0, 1.5f, 0), new Vector3(0, 1.5f, -9)), "walls stop the camera seeing through the café.");
            var path = new NavMeshPath();
            Check(NavMesh.SamplePosition(game.Counter.position, out var villageStart, 1.5f, NavMesh.AllAreas), "the café connects to village navigation.");
            var board = UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(item => item.Id == "speaking-board");
            var boardMesh = lodge.GetComponentsInChildren<MeshRenderer>().First(mesh => mesh.name == "Mission board");
            Check(Vector3.Distance(board.transform.position, boardMesh.bounds.center) < 0.3f, "mission interaction aligns with the imported Blender board after coordinate conversion.");
            var counterMesh = lodge.GetComponentsInChildren<MeshRenderer>().First(mesh => mesh.name == "Welcome counter");
            Check(Physics.OverlapSphere(counterMesh.bounds.center, 0.05f).Any(collider => collider.transform.IsChildOf(lodge.transform)), "the visible lodge counter has a physical boundary in the same position.");
            Check(game.Atmosphere.Pond.sharedMaterial.shader.name == "ContextSpeak/Forest Water", "the pond uses the animated, lit forest water material.");
            var pipeline = (UniversalRenderPipelineAsset)UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Check(pipeline.supportsSoftShadows && pipeline.shadowCascadeCount == 4, "forest rendering uses soft cascaded shadows.");
            Check(game.Player.View.GetComponent<UniversalAdditionalCameraData>().requiresDepthTexture, "water shoreline fading has a valid camera depth source.");
            var defaultRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Generated/Renderer.asset");
            Check(!defaultRenderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>().Any(), "forest contact occlusion does not leak into the default or arcade renderer.");
            var cameraData = game.Player.View.GetComponent<UniversalAdditionalCameraData>();
            var rendererIndex = new SerializedObject(cameraData).FindProperty("m_RendererIndex").intValue;
            Check(rendererIndex > 0 && !pipeline.supportsCameraDepthTexture, "the forest opts into its own renderer/depth without changing ordinary cameras.");
            Check(board.Arrival != null && Vector3.Distance(board.Arrival.position, board.transform.position) < board.Range, "lodge map travel arrives within board interaction range.");
            Check(NavMesh.SamplePosition(board.Arrival.position, out var lodgeArrival, 1, NavMesh.AllAreas)
                && NavMesh.CalculatePath(villageStart.position, lodgeArrival.position, NavMesh.AllAreas, path)
                && path.status == NavMeshPathStatus.PathComplete, "the lodge is reachable on foot from the café.");
            Check(!Physics.CheckCapsule(board.Arrival.position + Vector3.up * 0.3f, board.Arrival.position + Vector3.up * 1.4f, 0.28f), "the lodge arrival clears the player's capsule.");
            foreach (var npc in residents)
            {
                var bottom = npc.Arrival + Vector3.up * 0.3f;
                var top = npc.Arrival + Vector3.up * 1.4f;
                var blockers = Physics.OverlapCapsule(bottom, top, 0.28f).Where(collider => !collider.isTrigger && collider.GetComponentInParent<LearningNpc>() == null).ToArray();
                Check(blockers.Length == 0, npc.Place.Name + " arrival is clear of furniture, not merely near the NavMesh.");
                Check(NavMesh.SamplePosition(npc.Arrival, out var arrival, 1.5f, NavMesh.AllAreas), npc.Place.Name + " has a walkable arrival.");
                Check(NavMesh.CalculatePath(villageStart.position, arrival.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, npc.Place.Name + " is reachable on foot from the café.");
                Check(NavMesh.SamplePosition(npc.transform.position, out var actor, 2, NavMesh.AllAreas), npc.Place.Npc + " stands on walkable ground.");
                Check(NavMesh.CalculatePath(arrival.position, actor.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, npc.Place.Npc + " is physically reachable.");
            }
            Check(NavMesh.SamplePosition(game.Counter.position, out var counter, 1.5f, NavMesh.AllAreas), "the counter is on the NavMesh.");
            Check(NavMesh.SamplePosition(game.Seats[0].transform.position, out var table, 1.5f, NavMesh.AllAreas), "tables are reachable.");
            Check(NavMesh.CalculatePath(counter.position, table.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "the waiter can walk from the counter to the tables.");
            Check(NavMesh.SamplePosition(game.Waiter.Pass.position, out var pass, 1.5f, NavMesh.AllAreas) && NavMesh.CalculatePath(counter.position, pass.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "the waiter can reach the kitchen pass.");
            Check(NavMesh.SamplePosition(game.Pier.Stand.position, out var pier, 1.5f, NavMesh.AllAreas) && NavMesh.CalculatePath(counter.position, pier.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "the pier is reachable from the café.");
            var missingArt = CafeMenu.Items.Where(item => Resources.Load<Texture2D>("SliceMenu/" + item.Id) == null).Select(item => item.Id).ToArray();
            Check(missingArt.Length == 0, "menu pictures exist for every item (missing: " + string.Join(", ", missingArt) + ").");
            Debug.Log("Forest Café slice scene checks passed");
        }
    }
}
