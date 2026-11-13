using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Slice;
using ContextSpeak.Cafe;
using UnityEngine;
using static ContextSpeak.Editor.CafeScene;

namespace ContextSpeak.Editor
{
    /// Reference-led compositions, in metres. Each room has an open front and a reachable actor.
    public static class CozyWorldScene
    {
        private static readonly Color Oak = H("B68B60"), Walnut = H("624C3A"), Plaster = H("EAE0CC"), Sage = H("89967E"),
            Linen = H("E2D6BC"), Brass = H("B39051"), Green = H("55775A"), Rose = H("C78D8C"), Ink = H("33463F");
        private static Transform district;
        private static Vector3 origin;

        public static void Build()
        {
            Box("West village ground", new Vector3(-50, -0.25f, 6), new Vector3(40, 0.5f, 152), Sage, true);
            Box("East village ground", new Vector3(50, -0.25f, 6), new Vector3(40, 0.5f, 152), Sage, true);
            Box("South village ground", new Vector3(0, -0.25f, -45), new Vector3(60, 0.5f, 50), Sage, true);
            Box("North village ground", new Vector3(0, -0.25f, 61), new Vector3(60, 0.5f, 42), Sage, true);
            foreach (var surface in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name.Contains("village ground"))) EnvironmentArt.Surface(surface.gameObject);
            Path(new Vector3(-48, 0.025f, -16), new Vector3(48, 0.025f, -16));
            Path(new Vector3(-48, 0.025f, -16), new Vector3(-48, 0.025f, 33));
            Path(new Vector3(48, 0.025f, -16), new Vector3(48, 0.025f, 33));
            Path(new Vector3(0, 0.025f, -49), new Vector3(0, 0.025f, -16));
            Path(new Vector3(0, 0.025f, 28), new Vector3(0, 0.025f, 60));
            Path(new Vector3(-48, 0.025f, 33), new Vector3(48, 0.025f, 33));

            Path(new Vector3(-48, 0.025f, 33), new Vector3(-48, 0.025f, 60));
            Begin("Community classroom", new Vector3(-48, 0, 60)); School();
            Npc("school", new Vector3(0, 0.05f, -3), new Vector3(0, 0.1f, -0.7f));
            Begin("Family home", new Vector3(-48, 0, -25)); Home();
            Npc("home", new Vector3(1.5f, 0.05f, 0), new Vector3(1.5f, 0.1f, 2));
            Begin("Town market", new Vector3(48, 0, -25)); Market();
            Npc("market", new Vector3(2.5f, 0.05f, -1), new Vector3(2.5f, 0.1f, 1));
            Begin("Garden restaurant", new Vector3(-48, 0, 23)); Restaurant(false);
            Npc("restaurant", new Vector3(0, 0.05f, 0), new Vector3(0, 0.1f, 2));
            Begin("Sakura kitchen", new Vector3(48, 0, 23)); Restaurant(true);
            Npc("japan", new Vector3(0, 0.05f, 0), new Vector3(0, 0.1f, 2));
            Begin("Meadow park", new Vector3(0, 0, 60)); Park();
            Npc("park", new Vector3(5, 0.05f, 1), new Vector3(5, 0.1f, 3));
            Begin("Willow gallery", new Vector3(0, 0, -51)); Mall();
            Npc("mall", new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.1f, 1.5f));
            Begin("Forest Café learning", Vector3.zero);
            Npc("cafe", new Vector3(7.5f, 0.05f, -2.5f), new Vector3(7.5f, 0.1f, -0.5f));
            Begin("Fishing conversation", Vector3.zero);
            Npc("fishing", new Vector3(7.8f, 0.05f, 12.8f), new Vector3(7.8f, 0.1f, 11));
            ForestDetails();
            NpcCastCatalog.AddResidents();
        }

        private static void Begin(string name, Vector3 position)
        {
            district = new GameObject(name).transform;
            district.position = position;
            origin = position;
        }

        private static void Npc(string scene, Vector3 position, Vector3 arrival)
        {
            var actor = NpcCastCatalog.Create("ForestSlice", scene, origin + position);
            actor.name = LifeCurriculum.Find(scene).Npc + " · " + LifeCurriculum.Find(scene).Role;
            actor.transform.SetParent(district, true);
            var npc = actor.AddComponent<LearningNpc>();
            npc.SceneId = scene;
            npc.Arrival = origin + arrival;
            var body = actor.AddComponent<CapsuleCollider>();
            body.center = Vector3.up * 0.85f;
            body.height = 1.7f;
            body.radius = 0.25f;
            // Dynamic actors are excluded from static navigation geometry.
            body.enabled = false;
            Sign(LifeCurriculum.Find(scene).Name, origin + new Vector3(0, 2.8f, 6), 0.12f);
        }

        private static GameObject B(string name, Vector3 point, Vector3 size, Color colour, bool collide = false)
        {
            var item = Box(name, origin + point, size, colour, collide);
            item.transform.SetParent(district, true);
            if (colour == Oak || colour == Walnut)
                item.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Wood(colour);
            if (name.Contains("table") || name.Contains("sofa") || name.Contains("cabinet") || name.Contains("counter") || name.Contains("stall") || name.Contains("crate"))
                item.GetComponent<MeshFilter>().sharedMesh = CozyGeometry.RoundedBox();
            return item;
        }

        private static GameObject S(string name, Vector3 point, Vector3 size, Color colour)
        {
            var item = Primitive(name, PrimitiveType.Sphere, origin + point, size, colour, false);
            item.transform.SetParent(district, true);
            return item;
        }

        private static void Room(string scene, string name, float width = 17, float depth = 12)
        {
            B(name + " oak floor", new Vector3(0, 0.02f, 0), new Vector3(width, 0.06f, depth), Oak, true);
            B("Cream plaster wall", new Vector3(0, 1.9f, -depth / 2), new Vector3(width, 3.8f, 0.18f), Plaster, true);
            foreach (var side in new[] { -1, 1 })
            {
                B("Timber corner post", new Vector3(side * width / 2, 2, -depth / 2), new Vector3(0.22f, 4, 0.22f), Walnut);
                B("Lower window wall", new Vector3(side * width / 2, 0.4f, 0), new Vector3(0.18f, 0.8f, depth), Plaster, true);
                B("Window beam", new Vector3(side * width / 2, 3.4f, 0), new Vector3(0.18f, 0.18f, depth), Walnut);
                B("Window glazing", new Vector3(side * width / 2, 2, 0), new Vector3(0.04f, 2.4f, depth), Linen, true).GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glass();
                for (var z = -5; z <= 5; z += 2) B("Window mullion", new Vector3(side * width / 2, 2.1f, z), new Vector3(0.14f, 2.7f, 0.12f), Walnut);
            }
            for (var z = -5; z <= 5; z += 5)
            {
                Beam(new Vector3(-width / 2, 3.7f, z), new Vector3(0, 5.5f, z), 0.18f, Walnut);
                Beam(new Vector3(width / 2, 3.7f, z), new Vector3(0, 5.5f, z), 0.18f, Walnut);
            }
            B("Ridge beam", new Vector3(0, 5.5f, 0), new Vector3(0.22f, 0.22f, depth), Walnut);
            foreach (var side in new[] { -1, 1 })
            {
                var roof = B("Glass roof", new Vector3(side * width / 4, 4.6f, 0), new Vector3(Mathf.Sqrt(width * width / 4 + 3.24f), 0.04f, depth), Linen);
                roof.transform.rotation = Quaternion.Euler(0, 0, -side * Mathf.Atan2(1.8f, width / 2) * Mathf.Rad2Deg);
                roof.GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glass();
            }
            for (var x = -6; x <= 6; x += 6) Pendant(new Vector3(x, 3.3f, -1));
            foreach (var x in new[] { -7f, 7f }) Plant(new Vector3(x, 0, 4));
            BuildingArt.RoomSkin(scene, district, origin, width, depth);
        }

        private static void UsePoint(GameObject item, string id, string prompt, Vector3 arrival)
        {
            var interaction = item.AddComponent<Interactable>();
            interaction.Id = id;
            interaction.Prompt = prompt;
            interaction.Range = 2.6f;
            interaction.Arrival = new GameObject(id + " arrival").transform;
            interaction.Arrival.position = origin + arrival;
            interaction.Arrival.SetParent(district, true);
        }

        private static void School()
        {
            Room("school", "Community classroom");
            B("Chalkboard", new Vector3(0, 1.9f, -5.7f), new Vector3(5, 1.5f, 0.12f), Green);
            Sign("English conversation club", origin + new Vector3(0, 2.9f, -5.5f), 0.1f);
            for (var row = 0; row < 2; row++)
                foreach (var side in new[] { -1, 1 })
                {
                    var point = new Vector3(side * 3, 0.75f, row * 3);
                    B("Class desk", point, new Vector3(2.2f, 0.12f, 1.1f), Oak, true);
                    foreach (var leg in new[] { -1, 1 }) B("Desk leg", point + new Vector3(leg * 0.8f, -0.38f, 0), new Vector3(0.1f, 0.75f, 0.5f), Walnut, true);
                    B("Class chair", point + new Vector3(0, -0.3f, 1), new Vector3(0.7f, 0.12f, 0.7f), Sage);
                }
            var timetable = B("Class timetable", new Vector3(-6, 1.25f, 3.5f), new Vector3(1.3f, 1.8f, 0.15f), Linen, true);
            UsePoint(timetable, "timetable", "Read classroom timetable", new Vector3(-5.4f, 0.1f, 2));
            Plant(new Vector3(6.5f, 0, -4));
        }

        private static void Home()
        {
            Room("home", "Family home");
            for (var i = 0; i < 6; i++)
            {
                B("Sage cabinet", new Vector3(-6 + i * 2, 0.46f, -5), new Vector3(1.9f, 0.92f, 0.8f), Sage, true);
                B("Stone countertop", new Vector3(-6 + i * 2, 0.97f, -5), new Vector3(2, 0.1f, 1), Linen);
                B("Brass handle", new Vector3(-6 + i * 2, 0.7f, -4.57f), new Vector3(0.3f, 0.03f, 0.04f), Brass);
            }
            B("Kitchen island", new Vector3(1.5f, 0.47f, -2.5f), new Vector3(3.5f, 0.94f, 1.3f), Sage, true);
            B("Island stone top", new Vector3(1.5f, 0.99f, -2.5f), new Vector3(3.8f, 0.12f, 1.5f), Linen);
            B("Kitchen cooker hood", new Vector3(0, 2.65f, -5.6f), new Vector3(1.9f, 0.25f, 0.7f), H("A4A7A0"));
            B("Cooker chimney", new Vector3(0, 3.08f, -5.7f), new Vector3(0.5f, 0.75f, 0.35f), H("A4A7A0"));
            B("Cooktop", new Vector3(0, 1.045f, -5), new Vector3(1.3f, 0.035f, 0.75f), Ink);
            S("Cooking pot", new Vector3(0, 1.18f, -5), new Vector3(0.55f, 0.25f, 0.5f), H("8F9994"));
            foreach (var side in new[] { -1, 1 })
            {
                for (var shelf = 0; shelf < 2; shelf++)
                {
                    B("Kitchen oak shelf", new Vector3(side * 3.7f, 1.75f + shelf * 0.65f, -5.65f), new Vector3(3.2f, 0.09f, 0.5f), Oak);
                    for (var jar = 0; jar < 5; jar++)
                        S("Pantry jar", new Vector3(side * 3.7f - 1.2f + jar * 0.6f, 1.95f + shelf * 0.65f, -5.55f), new Vector3(0.25f, 0.35f, 0.25f), jar % 2 == 0 ? Linen : Sage);
                }
                Plant(new Vector3(side * 4.4f, 2.45f, -5.5f), 0.55f);
                B("Window curtain", new Vector3(side * 8.3f, 2.25f, 4), new Vector3(0.28f, 2.4f, 1), Linen);
            }
            B("Woven area rug", new Vector3(-4.5f, 0.065f, 2), new Vector3(5.4f, 0.02f, 4.5f), Linen);
            Sofa(new Vector3(-4.5f, 0, 0.2f));
            Table(new Vector3(-4.5f, 0, 3.2f), false);
            B("Refrigerator", new Vector3(6.7f, 1, -5), new Vector3(1.1f, 2, 1), H("B7BCB7"), true);
            B("Open bookshelf", new Vector3(7.4f, 1.2f, 0), new Vector3(0.4f, 2.4f, 3.5f), Walnut, true);
            for (var i = 0; i < 16; i++) B("Book spine", new Vector3(7.12f, 0.45f + i / 6 * 0.62f, -1.3f + i % 6 * 0.42f), new Vector3(0.24f, 0.4f, 0.28f), i % 3 == 0 ? Sage : i % 3 == 1 ? Rose : Linen);
            S("Sleeping cat", new Vector3(-4.9f, 0.65f, 0.2f), new Vector3(0.6f, 0.25f, 0.35f), H("BC8550"));
            S("Cat head", new Vector3(-4.65f, 0.7f, 0.22f), Vector3.one * 0.22f, H("BC8550"));
            Plant(new Vector3(5.7f, 0.99f, -5));
            var wardrobe = B("Family wardrobe", new Vector3(6.7f, 1.1f, 3.5f), new Vector3(1.8f, 2.2f, 0.7f), Oak, true);
            B("Wardrobe mirror", new Vector3(6.7f, 1.3f, 3.88f), new Vector3(0.65f, 1.5f, 0.025f), H("C5D9D3"));
            UsePoint(wardrobe, "wardrobe", "Edit your character at the wardrobe", new Vector3(5, 0.1f, 3.5f));
        }

        private static void Market()
        {
            Room("market", "Covered market", 21, 14);
            for (var side = -1; side <= 1; side += 2)
                for (var row = 0; row < 3; row++)
                {
                    var x = side * 6;
                    var z = -4 + row * 4;
                    B("Produce stall", new Vector3(x, 0.45f, z), new Vector3(3.5f, 0.9f, 2.5f), Oak, true);
                    for (var stripe = 0; stripe < 7; stripe++) B("Canvas awning stripe", new Vector3(x - 1.5f + stripe * 0.5f, 2.7f, z), new Vector3(0.5f, 0.08f, 3), stripe % 2 == 0 ? Rose : Linen);
                    for (var basket = 0; basket < 3; basket++)
                    {
                        var bx = x - 1.1f + basket * 1.1f;
                        B("Wooden produce crate", new Vector3(bx, 1, z), new Vector3(1, 0.2f, 1.6f), Walnut);
                        for (var item = 0; item < 8; item++) S("Fresh produce", new Vector3(bx - 0.3f + item % 2 * 0.55f, 1.2f, z - 0.5f + item / 2 * 0.33f), Vector3.one * 0.24f, basket == 0 ? H("BF5945") : basket == 1 ? H("D4AC4D") : H("749752"));
                    }
                }
            for (var i = 0; i < 7; i++) B("Central paving", new Vector3(0, 0.065f, -6 + i * 2), new Vector3(4, 0.04f, 1.9f), i % 2 == 0 ? Linen : H("C7B9A0"));
        }

        private static void Restaurant(bool japanese)
        {
            Room(japanese ? "japan" : "restaurant", japanese ? "Sakura kitchen" : "Garden restaurant");
            var cloth = japanese ? H("354A57") : Rose;
            B("Service counter", new Vector3(0, 0.52f, -4.8f), new Vector3(11, 1.04f, 1.5f), Walnut, true);
            for (var i = -2; i <= 2; i++)
            {
                B("Open shelving", new Vector3(i * 2, 2, -5.8f), new Vector3(1.8f, 0.1f, 0.4f), Oak);
                for (var cup = 0; cup < 3; cup++) S("Ceramic bowl", new Vector3(i * 2 - 0.5f + cup * 0.5f, 2.15f, -5.65f), new Vector3(0.3f, 0.17f, 0.3f), Linen);
            }
            foreach (var x in new[] { -4.5f, 4.5f })
                foreach (var z in new[] { 0f, 3.8f })
                {
                    Table(new Vector3(x, 0, z), true);
                    B("Table runner", new Vector3(x, 0.81f, z), new Vector3(0.7f, 0.02f, 1.4f), cloth);
                }
            if (japanese)
            {
                for (var i = 0; i < 7; i++)
                {
                    var p = new Vector3(-6 + i * 2, 2.7f, -3.5f);
                    S("Paper lantern", p, new Vector3(0.65f, 0.9f, 0.65f), i % 2 == 0 ? H("D8AA75") : H("C66C5A"));
                    B("Indigo noren", new Vector3(-6 + i * 2, 2.8f, -5.5f), new Vector3(1.8f, 0.7f, 0.04f), H("344856"));
                }
                for (var i = 0; i < 18; i++) S("Cherry blossom cluster", new Vector3(-7 + i % 3 * 0.45f, 2.8f + i / 3 * 0.15f, 4.5f), Vector3.one * 0.45f, H("E5B6C1"));
            }
        }

        private static void Park()
        {
            B("Park paving", new Vector3(0, 0.025f, 0), new Vector3(30, 0.05f, 23), H("D5C5A9"), true);
            var centre = new Vector3(-5, 8, -6);
            var wheel = new GameObject("Pastel Ferris wheel").transform;
            wheel.SetParent(district, false);
            wheel.localPosition = centre;
            var firstWheelPart = district.childCount;
            for (var i = 0; i < 12; i++)
            {
                var a = i * Mathf.PI * 2 / 12;
                var b = (i + 1) * Mathf.PI * 2 / 12;
                var from = centre + new Vector3(Mathf.Cos(a) * 6.5f, Mathf.Sin(a) * 6.5f, 0);
                var to = centre + new Vector3(Mathf.Cos(b) * 6.5f, Mathf.Sin(b) * 6.5f, 0);
                Beam(from, to, 0.16f, Linen);
                Beam(centre, from, 0.07f, Brass);
                B("Pastel gondola", from - Vector3.up * 0.4f, new Vector3(1.15f, 0.65f, 0.8f), i % 3 == 0 ? Sage : i % 3 == 1 ? Rose : H("C0B0C6"));
            }
            var wheelParts = Enumerable.Range(firstWheelPart, district.childCount - firstWheelPart).Select(index => district.GetChild(index)).ToArray();
            foreach (var part in wheelParts) { part.gameObject.isStatic = false; part.SetParent(wheel, true); }
            var motion = wheel.gameObject.AddComponent<CozySceneryMotion>();
            motion.KeepChildrenUpright = true;
            Beam(centre, new Vector3(-8, 0, -5), 0.3f, Linen);
            Beam(centre, new Vector3(-2, 0, -5), 0.3f, Linen);
            var carousel = S("Carousel canopy", new Vector3(8, 3.5f, -5), new Vector3(7, 1.3f, 7), Rose);
            B("Carousel base", new Vector3(8, 0.2f, -5), new Vector3(7, 0.4f, 7), Oak, true);
            for (var i = 0; i < 6; i++)
            {
                var a = i * Mathf.PI / 3;
                var p = new Vector3(8 + Mathf.Cos(a) * 2.4f, 1.6f, -5 + Mathf.Sin(a) * 2.4f);
                B("Carousel brass pole", p, new Vector3(0.06f, 3, 0.06f), Brass);
                S("Carousel pony", p - Vector3.up * 0.4f, new Vector3(0.6f, 0.65f, 1.1f), Linen);
                S("Pony head", p + new Vector3(0, 0.1f, 0.4f), new Vector3(0.4f, 0.55f, 0.35f), Linen);
            }
            B("Tickets kiosk", new Vector3(7, 0.6f, 3), new Vector3(3, 1.2f, 1.8f), Sage, true);
            for (var i = 0; i < 6; i++) S("Pastel balloon", new Vector3(9 + i % 2 * 0.5f, 2.5f + i / 2 * 0.5f, 3), new Vector3(0.45f, 0.6f, 0.45f), i % 2 == 0 ? Rose : Linen);
        }

        private static void Mall()
        {
            Room("mall", "Willow shopping atrium", 25, 17);
            var fitting = B("Fitting room mirror", new Vector3(-8, 1.2f, 5), new Vector3(1.3f, 2.4f, 0.15f), H("C5D9D3"), true);
            UsePoint(fitting, "fitting-room", "Preview your character in the fitting room", new Vector3(-7.2f, 0.1f, 3.5f));
            B("Atrium stone tiles", new Vector3(0, 0.06f, 0), new Vector3(25, 0.06f, 17), H("DBD9CE"));
            foreach (var side in new[] { -1, 1 })
            {
                B("Mezzanine", new Vector3(side * 9, 3.5f, -1), new Vector3(6, 0.18f, 15), Linen);
                B("Glass railing frame", new Vector3(side * 6, 4.05f, -1), new Vector3(0.08f, 1, 15), Brass);
                for (var i = 0; i < 6; i++)
                {
                    var x = side * (8 + i % 2 * 2);
                    var z = -5 + i / 2 * 4;
                    B("Display wardrobe rail", new Vector3(x, 1.4f, z), new Vector3(1.6f, 0.05f, 0.05f), Brass);
                    for (var garment = 0; garment < 3; garment++) B("Displayed knitwear", new Vector3(x - 0.5f + garment * 0.5f, 1.1f, z), new Vector3(0.35f, 0.55f, 0.1f), garment % 2 == 0 ? Sage : Rose);
                }
            }
            B("Escalator slope", new Vector3(3, 1.8f, -4), new Vector3(1.8f, 0.2f, 7.5f), Ink).transform.rotation = Quaternion.Euler(-28, 0, 0);
            Sofa(new Vector3(-1.5f, 0, 2));
            Plant(new Vector3(-1.5f, 0.65f, 0), 2.8f);
            Table(new Vector3(-1.5f, 0, 4.5f), false);
        }

        private static void ForestDetails()
        {
            Begin("Forest reference details", Vector3.zero);
            var architecture = new GameObject("Café greenhouse frame").transform;
            var before = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().ToHashSet();
            // Keep the café and delivery routes unchanged. Add high-value architectural details at their edges.
            for (var z = -6; z <= 6; z += 3)
            {
                Beam(new Vector3(-9, 3.7f, z), new Vector3(0, 6.4f, z), 0.22f, Walnut);
                Beam(new Vector3(9, 3.7f, z), new Vector3(0, 6.4f, z), 0.22f, Walnut);
            }
            B("Greenhouse ridge", new Vector3(0, 6.4f, 0), new Vector3(0.22f, 0.22f, 14), Walnut);
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (!before.Contains(item)) item.transform.SetParent(architecture, true);
            foreach (var item in district.Cast<Transform>().ToArray()) item.SetParent(architecture, true);
            BuildingArt.CafeFacade(architecture);
            for (var i = 0; i < 12; i++)
            {
                var x = -8.5f + i * 1.55f;
                B("Terrace flower box", new Vector3(x, 0.3f, 11.7f), new Vector3(1.1f, 0.4f, 0.42f), Oak);
                for (var petal = 0; petal < 4; petal++) S("Hydrangea cluster", new Vector3(x - 0.3f + petal * 0.2f, 0.64f, 11.7f), Vector3.one * 0.23f, petal % 2 == 0 ? Rose : Linen);
            }
            // Details stay outside the pier walking lane and do not block casting.
            B("Tackle box", new Vector3(5.5f, 0.4f, 16.3f), new Vector3(0.3f, 0.25f, 0.4f), Sage);
            B("Folded fishing stool", new Vector3(4.4f, 0.45f, 15.6f), new Vector3(0.35f, 0.06f, 0.4f), Linen);
            for (var i = 0; i < 6; i++)
                Plant(new Vector3(i % 2 == 0 ? -8.5f : 8.5f, 0, -5 + i / 2 * 4));
        }

        private static void Table(Vector3 p, bool chairs)
        {
            B("Rounded oak table top", p + Vector3.up * 0.78f, new Vector3(2, 0.1f, 1.4f), Oak, true);
            foreach (var x in new[] { -0.8f, 0.8f })
                foreach (var z in new[] { -0.5f, 0.5f }) B("Table tapered leg", p + new Vector3(x, 0.38f, z), new Vector3(0.08f, 0.76f, 0.08f), Walnut);
            Plant(p + Vector3.up * 0.84f, 0.3f);
            if (!chairs) return;
            foreach (var z in new[] { -1.1f, 1.1f })
            {
                B("Chair seat", p + new Vector3(0, 0.43f, z), new Vector3(0.6f, 0.1f, 0.6f), Oak, true);
                B("Chair back", p + new Vector3(0, 0.73f, z * 1.2f), new Vector3(0.6f, 0.6f, 0.07f), Oak);
            }
        }

        private static void Sofa(Vector3 p)
        {
            B("Linen sofa base", p + Vector3.up * 0.3f, new Vector3(3.2f, 0.5f, 1.1f), Linen, true);
            B("Linen sofa back", p + new Vector3(0, 0.72f, -0.45f), new Vector3(3.3f, 0.85f, 0.22f), Linen);
            foreach (var x in new[] { -1f, 0f, 1f }) S("Soft sofa cushion", p + new Vector3(x, 0.62f, 0), new Vector3(1, 0.23f, 0.9f), Linen);
            S("Sage cushion", p + new Vector3(-1, 0.86f, -0.2f), new Vector3(0.65f, 0.65f, 0.25f), Sage);
            S("Ochre cushion", p + new Vector3(1, 0.86f, -0.2f), new Vector3(0.65f, 0.65f, 0.25f), Brass);
        }

        private static void Plant(Vector3 p, float scale = 1)
        {
            S("Ceramic planter", p + Vector3.up * scale * 0.18f, new Vector3(0.5f, 0.4f, 0.5f) * scale, Linen);
            for (var i = 0; i < 5; i++)
            {
                var a = i * Mathf.PI * 2 / 5;
                var leaf = S("Broad leaf", p + new Vector3(Mathf.Cos(a) * 0.18f, 0.65f, Mathf.Sin(a) * 0.18f) * scale, new Vector3(0.3f, 0.65f, 0.12f) * scale, Green);
                leaf.transform.localRotation = Quaternion.Euler(25, i * 72, 25);
            }
        }

        private static void Pendant(Vector3 p)
        {
            B("Pendant cable", p + Vector3.up * 0.4f, new Vector3(0.025f, 0.8f, 0.025f), Ink);
            S("Brass pendant", p, new Vector3(0.85f, 0.4f, 0.85f), Brass);
            S("Warm diffuser", p - Vector3.up * 0.13f, new Vector3(0.6f, 0.035f, 0.6f), Linen).GetComponent<Renderer>().sharedMaterial = CozyMaterials.Glow();
        }

        private static void Beam(Vector3 a, Vector3 b, float width, Color colour)
        {
            var beam = B("Timber frame", (a + b) / 2, new Vector3(width, width, Vector3.Distance(a, b)), colour);
            beam.transform.rotation = Quaternion.LookRotation(b - a);
        }

        private static void Path(Vector3 a, Vector3 b)
        {
            var path = Box("Village walking path", (a + b) / 2, new Vector3(4, 0.04f, Vector3.Distance(a, b) + 2), H("C4B69D"), false);
            path.transform.rotation = Quaternion.LookRotation(b - a);
            EnvironmentArt.RoadSurface(path);
        }

        private static Color H(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var colour); return colour; }
    }
}
