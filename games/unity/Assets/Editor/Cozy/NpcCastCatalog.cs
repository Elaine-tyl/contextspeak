using System;
using System.IO;
using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Slice;
using UnityEngine;

namespace ContextSpeak.Editor
{
    /// Stable, inspectable casting; visual choices never change mission identities or saves.
    public static class NpcCastCatalog
    {
        [Serializable] public sealed class Actor { public string world, id, model; }
        [Serializable] public sealed class Resident { public string id, model, name, role; public float x, z; }
        [Serializable] public sealed class Catalog { public Actor[] actors; public Resident[] residents; }
        public static Catalog Read() => JsonUtility.FromJson<Catalog>(File.ReadAllText(MeshyCharacters.Source + "cast.json"));

        public static GameObject Create(string world, string id, Vector3 position, float yaw = 0)
        {
            var assignment = Read().actors.SingleOrDefault(actor => actor.world == world && actor.id == id);
            if (assignment == null) throw new InvalidOperationException("Missing NPC casting: " + world + "/" + id);
            var body = MeshyCharacters.Create(assignment.model, position);
            body.name = id;
            body.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return body;
        }

        public static GameObject CreateNpc(string world, string id, Vector3 position)
        {
            var body = Create(world, id, position);
            var collider = body.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.85f;
            collider.height = 1.7f;
            collider.radius = 0.25f;
            return body;
        }

        public static void AddResidents()
        {
            var group = new GameObject("Town neighbours").transform;
            var heights = MeshyCharacters.Specs.ToDictionary(spec => spec.id, spec => spec.height);
            foreach (var entry in Read().residents)
            {
                var body = MeshyCharacters.Create(entry.model, new Vector3(entry.x, 0.05f, entry.z));
                body.name = entry.name + " · " + entry.role;
                body.transform.SetParent(group, true);
                body.transform.rotation = Quaternion.Euler(0, 180, 0);
                var resident = body.AddComponent<LearningNpc>();
                resident.SceneId = entry.id;
                resident.NameOverride = entry.name;
                resident.RoleOverride = entry.role;
                resident.Arrival = new Vector3(entry.x, 0.1f, entry.z + 2.2f);
                var collider = body.AddComponent<CapsuleCollider>();
                collider.height = heights[entry.model];
                collider.center = Vector3.up * collider.height / 2;
                collider.radius = 0.25f;
                collider.enabled = false;
            }
        }
    }
}
