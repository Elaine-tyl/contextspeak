using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// One world map for the whole game, available in every scene with M (F10 also works).
    /// Every playable world from WorldSceneCatalog is a place on the same island map — your home (the villa, where
    /// a new game starts) in the middle, the Forest Café & lake, Metro City with the hospital and restaurant, the
    /// Living World district, the beach, the zoo, the airport and the overseas arrival airport. Click a place to see
    /// it in Chinese and English and travel there. In the Forest Café world the map also offers the detailed area map.
    public sealed class WorldMap : MonoBehaviour
    {
        private sealed class Place
        {
            public string Route, Zh, En, NoteZh, NoteEn; public Vector2 At; // At: 0..1 across the map
        }

        private static readonly Place[] Places =
        {
            new Place { Route = "villa", Zh = "我的家 · 豪华别墅", En = "Home · Luxury villa", NoteZh = "你的出生地：三层别墅、电梯、花园和车库。", NoteEn = "Where your story starts: three floors, a lift, garden and garage.", At = new Vector2(0.42f, 0.52f) },
            new Place { Route = "slice", Zh = "森林咖啡馆与湖", En = "Forest Café & lake", NoteZh = "点单、钓鱼和小镇居民的生活场景练习。", NoteEn = "Order with Rowan, fish with Hazel and practise with the townsfolk.", At = new Vector2(0.25f, 0.24f) },
            new Place { Route = "activities", Zh = "生活街区", En = "Living World district", NoteZh = "邻居们的日常生活、活动和随机事件。", NoteEn = "Neighbours' daily lives, activities and surprise events.", At = new Vector2(0.56f, 0.34f) },
            new Place { Route = "city", Zh = "都市", En = "Metro City", NoteZh = "商场、电车、巴士和城市生活。", NoteEn = "Malls, trams, buses and city life.", At = new Vector2(0.74f, 0.42f) },
            new Place { Route = "hospital", Zh = "医院", En = "Hospital", NoteZh = "挂号、描述症状、听医嘱。", NoteEn = "Check in, describe symptoms, follow the doctor's advice.", At = new Vector2(0.86f, 0.30f) },
            new Place { Route = "restaurant", Zh = "海港餐厅", En = "Harbour restaurant", NoteZh = "订位、点菜、提出要求、结账。", NoteEn = "Book, order, make requests and pay.", At = new Vector2(0.84f, 0.58f) },
            new Place { Route = "beach", Zh = "热带海滩度假村", En = "Tropical beach resort", NoteZh = "游泳、水上活动、海边小吃。", NoteEn = "Swimming, water sports and beach food.", At = new Vector2(0.50f, 0.82f) },
            new Place { Route = "zoo", Zh = "野生动物园", En = "Wildlife zoo", NoteZh = "认识动物、问路、和饲养员聊天。", NoteEn = "Animals, directions and chats with the keepers.", At = new Vector2(0.78f, 0.12f) },
            new Place { Route = "airport", Zh = "国际机场", En = "International airport", NoteZh = "值机、安检、登机、处理航班延误。", NoteEn = "Check in, security, boarding and flight delays.", At = new Vector2(0.14f, 0.62f) },
            new Place { Route = "cabin", Zh = "飞机机舱", En = "Aircraft cabin", NoteZh = "飞行途中和空乘对话。", NoteEn = "Talk to the cabin crew in flight.", At = new Vector2(0.10f, 0.86f) },
            new Place { Route = "arrival", Zh = "海外到达机场", En = "Arrival airport (overseas)", NoteZh = "入境、取行李、问交通。", NoteEn = "Immigration, baggage and getting into town.", At = new Vector2(0.28f, 0.92f) },
        };

        private static WorldMap instance;
        private VisualElement root, card;
        private Label cardTitle, cardNote, message;
        private Button go, areaMap;
        private Place selected;
        private bool open;
        private CursorLockMode lockBefore;
        private bool cursorBefore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            instance = new GameObject("World map (M)").AddComponent<WorldMap>();
            DontDestroyOnLoad(instance.gameObject);
        }

        public static void Toggle() { Create(); instance.SetOpen(!instance.open); }
        public static void Show() { Create(); instance.SetOpen(true); }
        public static bool IsOpen => instance != null && instance.open;

        private void Start()
        {
            root = CafeInterface.CreateRoot(gameObject);
            var document = GetComponent<UIDocument>();
            if (document != null && document.panelSettings != null) document.panelSettings.sortingOrder = 60;
            root.style.display = DisplayStyle.None;
            Build();
        }

        private void Build()
        {
            var shade = new VisualElement();
            shade.style.position = Position.Absolute; shade.style.left = 0; shade.style.right = 0; shade.style.top = 0; shade.style.bottom = 0;
            shade.style.backgroundColor = new Color(0.05f, 0.08f, 0.07f, 0.55f);
            shade.style.alignItems = Align.Center; shade.style.justifyContent = Justify.Center;
            root.Add(shade);

            var frame = new VisualElement();
            frame.style.width = Length.Percent(82); frame.style.height = Length.Percent(84); frame.style.maxWidth = 1180;
            frame.style.backgroundColor = new Color(0.97f, 0.96f, 0.92f); Round(frame, 18); frame.style.paddingLeft = 18; frame.style.paddingRight = 18; frame.style.paddingTop = 14; frame.style.paddingBottom = 14;
            shade.Add(frame);

            var header = Row(frame);
            var title = new Label("世界地图 · World map"); title.style.fontSize = 26; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.color = new Color(0.1f, 0.3f, 0.22f); title.style.flexGrow = 1;
            header.Add(title);
            var close = new Button(() => SetOpen(false)) { text = "关闭 Close (M)" }; header.Add(close);
            var hint = new Label("点击地点查看并前往 · Click a place to travel. 你的家是别墅，新游戏从这里开始 · Your home is the villa — every new game starts there.");
            hint.style.fontSize = 14; hint.style.color = new Color(0.32f, 0.38f, 0.35f); hint.style.marginBottom = 8; hint.style.whiteSpace = WhiteSpace.Normal;
            frame.Add(hint);

            var body = Row(frame); body.style.flexGrow = 1; body.style.alignItems = Align.Stretch;
            var map = new VisualElement(); map.style.flexGrow = 1; Round(map, 14); map.style.overflow = Overflow.Hidden;
            map.generateVisualContent += Paint;
            body.Add(map);

            foreach (var place in Places)
            {
                if (WorldSceneCatalog.All.All(scene => scene.Route != place.Route)) continue;
                var pin = new Button(() => Select(place)) { text = place.Zh + "\n" + place.En, name = "pin-" + place.Route };
                pin.style.position = Position.Absolute; pin.style.left = Length.Percent(place.At.x * 100); pin.style.top = Length.Percent(place.At.y * 100);
                pin.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
                pin.style.fontSize = 13; pin.style.whiteSpace = WhiteSpace.Normal; pin.style.unityTextAlign = TextAnchor.MiddleCenter;
                pin.style.backgroundColor = place.Route == "villa" ? new Color(0.72f, 0.36f, 0.16f) : new Color(0.09f, 0.41f, 0.31f);
                pin.style.color = Color.white; Round(pin, 12); pin.style.paddingLeft = 10; pin.style.paddingRight = 10; pin.style.paddingTop = 6; pin.style.paddingBottom = 6;
                pin.style.borderTopWidth = pin.style.borderBottomWidth = pin.style.borderLeftWidth = pin.style.borderRightWidth = 0;
                map.Add(pin);
            }

            card = new VisualElement(); card.style.width = 300; card.style.marginLeft = 14; card.style.backgroundColor = Color.white; Round(card, 14);
            card.style.paddingLeft = 16; card.style.paddingRight = 16; card.style.paddingTop = 14; card.style.paddingBottom = 14;
            body.Add(card);
            cardTitle = new Label(); cardTitle.style.fontSize = 20; cardTitle.style.unityFontStyleAndWeight = FontStyle.Bold; cardTitle.style.whiteSpace = WhiteSpace.Normal; cardTitle.style.color = new Color(0.1f, 0.3f, 0.22f);
            cardNote = new Label(); cardNote.style.fontSize = 15; cardNote.style.whiteSpace = WhiteSpace.Normal; cardNote.style.marginTop = 8; cardNote.style.color = new Color(0.25f, 0.3f, 0.28f);
            go = new Button(Travel) { text = "前往 Go" }; go.style.marginTop = 14; go.style.minHeight = 44; go.style.fontSize = 17;
            areaMap = new Button(() => { SetOpen(false); FindFirstObjectByType<ContextSpeak.Slice.SliceGame>()?.ShowAreaMap(); }) { text = "本区详细地图 · Area map" };
            areaMap.style.marginTop = 6;
            message = new Label(); message.style.whiteSpace = WhiteSpace.Normal; message.style.marginTop = 10; message.style.color = new Color(0.65f, 0.25f, 0.12f);
            card.Add(cardTitle); card.Add(cardNote); card.Add(go); card.Add(areaMap); card.Add(message);
        }

        // Painted island: sea, land, a lake, the beach strip, an overseas islet and roads between the places.
        private static void Paint(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect; var w = r.width; var h = r.height;
            Vector2 P(float x, float y) => new Vector2(x * w, y * h);
            var p = context.painter2D;
            p.fillColor = new Color(0.62f, 0.80f, 0.86f); p.BeginPath(); p.MoveTo(P(0, 0)); p.LineTo(P(1, 0)); p.LineTo(P(1, 1)); p.LineTo(P(0, 1)); p.ClosePath(); p.Fill();
            void Blob(Color c, params Vector2[] pts) { p.fillColor = c; p.BeginPath(); p.MoveTo(P(pts[0].x, pts[0].y)); foreach (var q in pts.Skip(1)) p.LineTo(P(q.x, q.y)); p.ClosePath(); p.Fill(); }
            Blob(new Color(0.94f, 0.87f, 0.66f), new Vector2(0.08f, 0.12f), new Vector2(0.55f, 0.04f), new Vector2(0.95f, 0.06f), new Vector2(0.97f, 0.70f), new Vector2(0.70f, 0.92f), new Vector2(0.30f, 0.90f), new Vector2(0.05f, 0.74f), new Vector2(0.03f, 0.40f));
            Blob(new Color(0.66f, 0.80f, 0.55f), new Vector2(0.11f, 0.15f), new Vector2(0.55f, 0.08f), new Vector2(0.92f, 0.10f), new Vector2(0.93f, 0.66f), new Vector2(0.68f, 0.76f), new Vector2(0.32f, 0.76f), new Vector2(0.08f, 0.70f), new Vector2(0.07f, 0.40f));
            Blob(new Color(0.40f, 0.62f, 0.44f), new Vector2(0.12f, 0.16f), new Vector2(0.38f, 0.11f), new Vector2(0.40f, 0.36f), new Vector2(0.14f, 0.40f));
            Blob(new Color(0.52f, 0.74f, 0.85f), new Vector2(0.28f, 0.30f), new Vector2(0.36f, 0.29f), new Vector2(0.37f, 0.36f), new Vector2(0.29f, 0.37f));
            Blob(new Color(0.78f, 0.78f, 0.76f), new Vector2(0.66f, 0.22f), new Vector2(0.93f, 0.20f), new Vector2(0.93f, 0.64f), new Vector2(0.66f, 0.62f));
            Blob(new Color(0.66f, 0.80f, 0.55f), new Vector2(0.18f, 0.86f), new Vector2(0.38f, 0.84f), new Vector2(0.40f, 0.98f), new Vector2(0.16f, 0.98f));
            p.strokeColor = new Color(0.55f, 0.48f, 0.38f, 0.9f); p.lineWidth = 5; p.lineCap = LineCap.Round;
            void Road(params string[] routes)
            {
                var pts = routes.Select(route => Places.First(x => x.Route == route).At).ToArray();
                p.BeginPath(); p.MoveTo(P(pts[0].x, pts[0].y)); foreach (var q in pts.Skip(1)) p.LineTo(P(q.x, q.y)); p.Stroke();
            }
            Road("slice", "villa", "activities", "city", "hospital"); Road("city", "restaurant"); Road("villa", "beach"); Road("villa", "airport");
            Road("activities", "zoo");
            p.strokeColor = new Color(0.25f, 0.35f, 0.55f, 0.7f); p.lineWidth = 2;
            var a = Places.First(x => x.Route == "airport").At; var b = Places.First(x => x.Route == "arrival").At;
            p.BeginPath(); p.MoveTo(P(a.x, a.y)); p.ArcTo(P(0.05f, 0.80f), P(b.x, b.y), 60); p.LineTo(P(b.x, b.y)); p.Stroke();
        }

        private void Select(Place place)
        {
            selected = place;
            var here = SceneManager.GetActiveScene().name == WorldSceneCatalog.All.First(scene => scene.Route == place.Route).Name;
            cardTitle.text = place.Zh + "\n" + place.En + (here ? "\n● 你在这里 · You are here" : "");
            cardNote.text = place.NoteZh + "\n" + place.NoteEn;
            go.text = here ? "你已在这里 · You are here" : "前往 Go";
            go.SetEnabled(!here);
            areaMap.style.display = here && FindFirstObjectByType<ContextSpeak.Slice.SliceGame>() != null ? DisplayStyle.Flex : DisplayStyle.None;
            message.text = "";
        }

        private void Travel()
        {
            if (selected == null) return;
            var name = WorldSceneCatalog.All.First(scene => scene.Route == selected.Route).Name;
            message.text = "正在前往 Loading " + selected.En + "…";
            WorldScenes.Open(name, error => { SetOpen(true); message.text = "无法前往 Couldn't travel: " + error; });
            SetOpen(false);
        }

        private void SetOpen(bool value)
        {
            if (root == null || open == value) return;
            open = value;
            root.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                lockBefore = UnityEngine.Cursor.lockState; cursorBefore = UnityEngine.Cursor.visible;
                UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
                var current = SceneManager.GetActiveScene().name;
                var here = Places.FirstOrDefault(place => WorldSceneCatalog.All.Any(scene => scene.Route == place.Route && scene.Name == current)) ?? Places[0];
                Select(here);
                foreach (var place in Places)
                {
                    var pin = root.Q<Button>("pin-" + place.Route);
                    if (pin == null) continue;
                    var isHere = place == here && WorldSceneCatalog.All.Any(scene => scene.Route == place.Route && scene.Name == current);
                    pin.style.borderTopWidth = pin.style.borderBottomWidth = pin.style.borderLeftWidth = pin.style.borderRightWidth = isHere ? 3 : 0;
                    pin.style.borderTopColor = pin.style.borderBottomColor = pin.style.borderLeftColor = pin.style.borderRightColor = new Color(1f, 0.85f, 0.3f);
                }
            }
            else { UnityEngine.Cursor.lockState = lockBefore; UnityEngine.Cursor.visible = cursorBefore; }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || root == null) return;
            if (open && keyboard.escapeKey.wasPressedThisFrame) { SetOpen(false); return; }
            if (keyboard.f10Key.wasPressedThisFrame) { SetOpen(!open); return; }
            if (!keyboard.mKey.wasPressedThisFrame) return;
            // The Forest Café world handles M itself (SliceGame opens this map); never toggle while typing in a field.
            if (FindFirstObjectByType<ContextSpeak.Slice.SliceGame>() != null) return;
            var focused = root.panel?.focusController?.focusedElement;
            if (focused is TextField) return;
            SetOpen(!open);
        }

        private static VisualElement Row(VisualElement parent) { var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; parent.Add(row); return row; }
        private static void Round(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }
    }
}
