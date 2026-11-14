using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Life;
using UnityEditor;
using UnityEngine;
using static ContextSpeak.Editor.LifeBuild;

namespace ContextSpeak.Editor
{
    /// Airport building blocks: PBR palette, glass curtain walls, steel, stanchion queues, signs,
    /// imported vehicles/aircraft (with labelled placeholders when an asset is missing), spots.
    public static class AirportKit
    {
        public static readonly Color Stone = Hex("D9D6D0"), DarkStone = Hex("8E8A84"), Steel = Hex("A9B0B7"), Aluminium = Hex("C7CCD1"), Concrete = Hex("B5B2AC"),
            Asphalt = Hex("3B3D40"), Kerb = Hex("A3A19C"), LinePaint = Hex("F2F2EE"), Yellow = Hex("F2C14E"), Brand = Hex("0F3D8C"), Teal = Hex("009E99"),
            Glass = new Color(0.72f, 0.85f, 0.92f, 0.28f), Grass = Hex("6F8F55"), Wood = Hex("B08458"), ScreenBlack = Hex("101820");

        public static GameObject Floor(string name, Vector3 centre, Vector2 size, Color colour, float smooth = 0.75f) =>
            Paint(Box(name, centre + Vector3.down * 0.025f, new Vector3(size.x, 0.05f, size.y), colour, true), colour, smooth);

        public static GameObject Steelwork(string name, Vector3 centre, Vector3 size) => Paint(Box(name, centre, size, Steel, false), Steel, 0.55f, 0.85f);

        /// A glass curtain wall from a to b with aluminium mullions; optional door gaps (centre along the wall, width).
        public static void Curtain(string name, Vector3 a, Vector3 b, float height, params Vector2[] gaps)
        {
            var direction = b - a; var length = direction.magnitude; var along = direction / length;
            var yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg - 90;
            var start = 0f;
            void Pane(float from, float to)
            {
                if (to - from < 0.05f) return;
                var middle = a + along * ((from + to) / 2);
                Box(name, middle + Vector3.up * height / 2, new Vector3(to - from, height, 0.06f), Glass, true, null, yaw);
                for (var m = from; m <= to + 0.01f; m += 2.5f)
                    Paint(Box("Mullion", a + along * Mathf.Min(m, to) + Vector3.up * height / 2, new Vector3(0.08f, height, 0.14f), Aluminium, false, null, yaw), Aluminium, 0.6f, 0.9f);
            }
            foreach (var gap in gaps.OrderBy(gap => gap.x))
            {
                Pane(start, gap.x - gap.y / 2);
                var lintel = a + along * gap.x + Vector3.up * (2.8f + (height - 2.8f) / 2);
                Box(name + " transom", lintel, new Vector3(gap.y, height - 2.8f, 0.06f), Glass, true, null, yaw);
                start = gap.x + gap.y / 2;
            }
            Pane(start, length);
            Paint(Box(name + " head", a + along * length / 2 + Vector3.up * height, new Vector3(length, 0.3f, 0.3f), Aluminium, false, null, yaw), Aluminium, 0.6f, 0.9f);
        }

        /// Retractable-belt stanchions in a serpentine queue; returns the slot positions.
        public static Transform[] Serpentine(string name, Vector3 start, Vector3 forward, int lanes, float laneLength, float laneWidth = 1.2f)
        {
            var root = Group(name, Vector3.zero);
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var slots = new List<Transform>();
            for (var lane = 0; lane <= lanes; lane++)
                for (var step = 0f; step <= laneLength; step += 1.5f)
                {
                    var post = start + right * (lane * laneWidth - laneWidth / 2) + forward * step;
                    if (lane > 0 && lane < lanes && (lane % 2 == 1 ? step > laneLength - 1.4f : step < 1.4f)) continue;
                    Paint(Shape("Stanchion", PrimitiveType.Cylinder, post + Vector3.up * 0.48f, new Vector3(0.06f, 0.48f, 0.06f), Aluminium, false, root), Aluminium, 0.7f, 0.9f);
                    if (step + 1.5f <= laneLength)
                        Box("Queue belt", post + forward * 0.75f + Vector3.up * 0.92f, new Vector3(0.02f, 0.05f, 1.5f), Brand, false, root, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
                }
            for (var lane = 0; lane < lanes; lane++)
                for (var step = 0.75f; step < laneLength; step += 1.6f)
                {
                    var along = lane % 2 == 0 ? step : laneLength - step;
                    var slot = Group("Queue slot", start + right * (lane * laneWidth) + forward * along, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg + (lane % 2 == 0 ? 0 : 180), root);
                    slots.Add(slot);
                }
            slots.Reverse();   // front of the queue first
            return slots.ToArray();
        }

        public static AirportSpot Spot(string action, string prompt, Vector3 position, float range = 2.4f)
        {
            var spot = new GameObject("Spot · " + action).AddComponent<AirportSpot>();
            spot.transform.position = position;
            spot.Action = action;
            spot.Prompt = prompt;
            spot.Range = range;
            return spot;
        }

        /// A screen with glowing text (flight boards, gate monitors). yaw: text faces viewers on −Z when 0.
        public static TextMesh ScreenBoard(string name, string text, Vector3 position, float yaw, Vector2 size, Color ink)
        {
            var panel = Box(name, position, new Vector3(size.x, size.y, 0.1f), ScreenBlack, false, null, yaw);
            var renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = Surface(ScreenBlack, 0.9f);
            var light = Group(name + " glow", position + Quaternion.Euler(0, yaw, 0) * Vector3.back * 0.6f);
            var glow = light.gameObject.AddComponent<Light>();
            glow.type = LightType.Point; glow.range = Mathf.Max(size.x, size.y) * 1.2f; glow.intensity = 0.6f; glow.color = new Color(0.6f, 0.75f, 1f); glow.shadows = LightShadows.None;
            var lines = Mathf.Max(1, text.Split('\n').Length);
            var label = Label(text, position + Quaternion.Euler(0, yaw, 0) * Vector3.back * 0.06f, yaw, Mathf.Min(size.y / (lines + 1.5f), size.x / 40f) / 2.2f * 1.0f, ink);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.anchor = TextAnchor.MiddleCenter;
            return label;
        }

        /// An imported model (glTF from Assets/Art), grounded and scaled to a real height; placeholder if missing.
        public static Transform Model(string file, Vector3 position, float yaw, float height, string placeholder)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
            GameObject item;
            if (prefab == null)
            {
                Missing.Add(file + " — PLACEHOLDER used for " + placeholder);
                item = Box("PLACEHOLDER · " + placeholder, position + Vector3.up * height / 2, new Vector3(height * 0.8f, height, height * 2.4f), Hex("FF00FF"), true);
                item.transform.rotation = Quaternion.Euler(0, yaw, 0);
                return item.transform;
            }
            var root = new GameObject(placeholder).transform;
            root.position = position;
            item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            item.transform.SetParent(root, false);
            var bounds = Measure(item);
            if (bounds.size.y > 0.01f) item.transform.localScale *= height / bounds.size.y;
            bounds = Measure(item);
            item.transform.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            root.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.isStatic = false;
            return root;
        }

        public static Bounds Measure(GameObject item)
        {
            var renderers = item.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(item.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// A street lamp named like Metro City's, so the sky system switches it on at dusk.
        public static void StreetLamp(Vector3 foot, float yaw)
        {
            var root = Group("Street lamp", foot, yaw);
            Paint(Shape("Lamp post", PrimitiveType.Cylinder, new Vector3(0, 3.5f, 0), new Vector3(0.14f, 3.5f, 0.14f), Hex("2B3036"), true, root), Hex("2B3036"), 0.5f, 0.6f);
            Box("Lamp arm", new Vector3(0, 6.9f, 0.7f), new Vector3(0.1f, 0.1f, 1.4f), Hex("2B3036"), false, root);
            var head = Box("Lamp head", new Vector3(0, 6.8f, 1.35f), new Vector3(0.5f, 0.12f, 0.35f), Hex("FFF6E0"), false, root);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.EnableKeyword("_EMISSION");
            material.SetColor("_BaseColor", Hex("FFF6E0"));
            material.SetColor("_EmissionColor", Hex("FFE2B0") * 0.2f);
            var path = Data + "/Materials/AirportLamp.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) == null) AssetDatabase.CreateAsset(material, path);
            head.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        public static void Invisible(string name, Vector3 centre, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.position = centre;
            wall.AddComponent<BoxCollider>().size = size;
            wall.isStatic = true;
        }

        public static void Bench(Vector3 floor, float yaw, int seats, string group, Color colour)
        {
            var bench = Group("Gate seating", floor, yaw);
            Paint(Box("Beam", new Vector3(0, 0.3f, 0), new Vector3(seats * 0.6f, 0.08f, 0.12f), Aluminium, false, bench), Aluminium, 0.6f, 0.9f);
            for (var i = 0; i < seats; i++)
            {
                var x = (i - (seats - 1) / 2f) * 0.6f;
                Box("Seat pad", new Vector3(x, 0.45f, 0), new Vector3(0.52f, 0.06f, 0.48f), colour, false, bench);
                Box("Seat back", new Vector3(x, 0.75f, -0.24f), new Vector3(0.52f, 0.55f, 0.05f), colour, false, bench);
                var seat = Group("Seat", new Vector3(x, 0.05f, 0), 0, bench).gameObject.AddComponent<SeatController>();
                seat.Group = group;
                seat.Prompt = "Sit down";
                seat.Range = 1.3f;
            }
        }
    }
}
