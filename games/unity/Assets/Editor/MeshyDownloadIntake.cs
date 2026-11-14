using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ContextSpeak.Editor
{
    /// Explicit requests import new downloads without reopening or saving the artist's scene.
    [InitializeOnLoad]
    public static class MeshyDownloadIntake
    {
        private const string Folder = "../../.local/meshy/intake-20261113/";
        [Serializable] private sealed class Request { public string[] hashes, characters; }
        [Serializable] private sealed class Result { public bool passed; public int imported, library, clips; public string error; }
        static MeshyDownloadIntake() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (!File.Exists(Folder + "apply.json") || EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var request = JsonUtility.FromJson<Request>(File.ReadAllText(Folder + "apply.json"));
            File.Delete(Folder + "apply.json");
            var result = new Result();
            File.WriteAllText(Folder + "unity-progress.txt", "Native import started");
            try
            {
                MeshyLibrary.ImportAndVerify();
                File.WriteAllText(Folder + "unity-progress.txt", "Native source library verified");
                MotionAssets.BuildAndVerify();
                foreach (var name in request.characters)
                    MeshyCharacters.CharacterPrefab(name, MeshyCharacters.Folder + name + ".prefab");
                MeshyChecks.Verify();
                File.WriteAllText(Folder + "unity-progress.txt", "Character geometry and animation verified");
                var entries = WorldAssetBuilder.Read().Where(entry => request.hashes.Contains(entry.sourceHash)).ToArray();
                PlaceNewEntries(entries);
                WorldAssetBuilder.BuildEntries(entries);
                WorldAssetBuilder.Addressables();
                AssetDatabase.SaveAssets();
                result.imported = request.hashes.Length;
                result.library = MeshyLibrary.Read().Length;
                result.clips = MotionAssets.Names.Sum(name => MotionAssets.Roles(name).Length);
                result.passed = true;
            }
            catch (Exception error) { result.error = error.ToString(); Debug.LogException(error); }
            File.WriteAllText(Folder + "unity-report.json", JsonUtility.ToJson(result, true));
            Debug.Log("New Meshy intake: " + (result.passed ? "verified" : "failed"));
        }

        private static void PlaceNewEntries(SceneryEntry[] additions)
        {
            var existing = WorldAssetBuilder.Read().Where(entry => entry.usage == "world"
                && !additions.Any(item => item.id == entry.id)).ToList();
            var candidates = new List<Vector3>();
            for (var x = -65; x <= 65; x += 3)
                for (var z = -65; z <= 77; z += 3) candidates.Add(new Vector3(x, .05f, z));
            var json = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(WorldAssetBuilder.Source + "catalog.json"));
            Physics.SyncTransforms();
            foreach (var entry in additions.Where(item => item.usage == "world").OrderByDescending(item => item.Size))
            {
                var radius = entry.Size / 2 + .35f;
                var centre = entry.zone == "Vehicles" ? new Vector3(57, 0, -58)
                    : entry.zone == "Aquarium" ? new Vector3(38, 0, 62) : new Vector3(23, 0, 70);
                var found = false;
                foreach (var point in candidates.OrderBy(point => Vector3.SqrMagnitude(point - centre)))
                {
                    if (Mathf.Abs(point.x) < 29 && point.z > -21 && point.z < 41) continue;
                    if (existing.Any(other => Vector3.Distance(point, other.Point) < radius + other.Size / 2 + 1.8f)) continue;
                    if (Physics.CheckBox(point + Vector3.up, new Vector3(radius, .7f, radius), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!NavMesh.SamplePosition(point, out _, .25f, NavMesh.AllAreas)) continue;
                    entry.position = new[] { point.x, point.y, point.z };
                    existing.Add(entry);
                    json.First(row => row["id"].ToString() == entry.id)["position"] = new Newtonsoft.Json.Linq.JArray(entry.position);
                    found = true;
                    break;
                }
                if (!found) throw new Exception("No safe new scenery placement: " + entry.id);
            }
            File.WriteAllText(WorldAssetBuilder.Source + "catalog.json", json.ToString() + "\n");
            AssetDatabase.ImportAsset(WorldAssetBuilder.Source + "catalog.json", ImportAssetOptions.ForceUpdate);
        }
    }
}
