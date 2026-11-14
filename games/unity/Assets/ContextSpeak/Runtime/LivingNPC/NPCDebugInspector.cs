using System.Linq;
using ContextSpeak.Cafe;
using ContextSpeak.Cafe.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace ContextSpeak.Life
{
    /// In-game NPC Debug Inspector (F6) and crowd-performance overlay (F7).
    /// F6: the NPC nearest the centre of the screen is selected ([ and ] cycle through all); shows
    /// identity, place, activity, goal, next scheduled block, needs, relationship, animation, navigation
    /// target, tier, event and dialogue state; buttons advance time, toggle rain, trigger a bump or a
    /// directions request, send the NPC to work/home, run an animation test, draw the path and anchors.
    public sealed class NPCDebugInspector : MonoBehaviour
    {
        private VisualElement panel, overlay;
        private Label info, crowd;
        private LivingNPC selected;
        private int index = -1;
        private LineRenderer path;
        private bool showPath, showAnchors;
        private float refresh;
        private int testAction;
        private readonly System.Collections.Generic.List<GameObject> anchorMarkers = new System.Collections.Generic.List<GameObject>();

        private void Start()
        {
            var root = GameUiRoot.Attach(gameObject);
            panel = Box(root, 16, 120, 380);
            var title = new Label("NPC inspector  ·  F6   ([ ] select)"); title.style.color = Color.white; title.style.unityFontStyleAndWeight = FontStyle.Bold; title.style.marginBottom = 6;
            panel.Add(title);
            info = new Label(); info.style.color = new Color(0.86f, 0.93f, 0.95f); info.style.fontSize = 12; info.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(info);
            var row1 = Row(panel); var row2 = Row(panel); var row3 = Row(panel);
            Add(row1, "+1 h", () => LivingClock.Advance(1));
            Add(row1, "Rain", () => { if (SkySystem.Active != null) SkySystem.Active.SetWeather(SkyState.Raining ? SkyWeather.Clear : SkyWeather.Rain, 4); });
            Add(row1, "Bump", () => { if (selected != null) FindFirstObjectByType<NPCEventDirector>()?.TryBump(selected); });
            Add(row1, "Directions", () => { if (selected != null) FindFirstObjectByType<NPCEventDirector>()?.TryDirections(selected); });
            Add(row2, "To work", () => selected?.SendTo("work"));
            Add(row2, "Home", () => selected?.SendTo("home"));
            Add(row2, "Anim test", () => { if (selected == null) return; var values = (NPCAction[])System.Enum.GetValues(typeof(NPCAction)); testAction = (testAction + 1) % values.Length; selected.Animation.Play(values[testAction]); });
            Add(row3, "Path", () => showPath = !showPath);
            Add(row3, "Anchors", () => { showAnchors = !showAnchors; Anchors(); });
            panel.style.display = DisplayStyle.None;
            root.Add(panel);
            overlay = Box(root, 16, 16, 300);
            overlay.style.top = StyleKeyword.Auto; overlay.style.bottom = 16; overlay.style.left = StyleKeyword.Auto; overlay.style.right = 16;
            crowd = new Label(); crowd.style.color = new Color(0.86f, 0.93f, 0.95f); crowd.style.fontSize = 12;
            overlay.Add(crowd);
            overlay.style.display = DisplayStyle.None;
            root.Add(overlay);
            path = new GameObject("NPC path").AddComponent<LineRenderer>();
            path.widthMultiplier = 0.06f; path.material = new Material(Shader.Find("Sprites/Default")); path.startColor = path.endColor = new Color(1, 0.8f, 0.2f);
            path.enabled = false;
        }

        private static VisualElement Box(VisualElement root, float left, float top, float width)
        {
            var box = new VisualElement();
            box.style.position = Position.Absolute; box.style.left = left; box.style.top = top; box.style.width = width;
            box.style.backgroundColor = new Color(0.05f, 0.08f, 0.1f, 0.88f);
            box.style.paddingLeft = box.style.paddingRight = 12; box.style.paddingTop = box.style.paddingBottom = 10;
            box.style.borderTopLeftRadius = box.style.borderTopRightRadius = box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 10;
            return box;
        }

        private static VisualElement Row(VisualElement parent) { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; parent.Add(r); return r; }
        private static void Add(VisualElement row, string text, System.Action action) { var b = new Button(action) { text = text }; b.style.minHeight = 28; b.style.fontSize = 12; b.style.paddingLeft = b.style.paddingRight = 8; row.Add(b); }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || panel == null) return;
            if (keyboard.f6Key.wasPressedThisFrame) panel.style.display = panel.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (keyboard.f7Key.wasPressedThisFrame) overlay.style.display = overlay.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            var open = panel.style.display == DisplayStyle.Flex;
            if (open && (keyboard.leftBracketKey.wasPressedThisFrame || keyboard.rightBracketKey.wasPressedThisFrame) && LivingNPC.All.Count > 0)
            {
                index = (index + (keyboard.rightBracketKey.wasPressedThisFrame ? 1 : -1) + LivingNPC.All.Count) % LivingNPC.All.Count;
                selected = LivingNPC.All[index];
            }
            if (open && selected == null) selected = Centre();
            path.enabled = open && showPath && selected != null;
            if (path.enabled)
            {
                var corners = selected.Navigation.Corners();
                path.positionCount = corners.Length + 1;
                path.SetPosition(0, selected.transform.position + Vector3.up * 0.1f);
                for (var i = 0; i < corners.Length; i++) path.SetPosition(i + 1, corners[i] + Vector3.up * 0.1f);
            }
            if ((refresh -= Time.unscaledDeltaTime) > 0) return;
            refresh = 0.2f;
            if (open && selected != null) info.text = Describe(selected);
            var manager = NPCLifeSimulationManager.Instance;
            if (overlay.style.display == DisplayStyle.Flex && manager != null)
                crowd.text = "Crowd  ·  F7\nNPCs " + LivingNPC.All.Count + "  ·  full " + manager.TierCounts[1] + " · background " + manager.TierCounts[2] + " · abstract " + manager.TierCounts[3] +
                             "\nSimulation " + manager.MeasuredMilliseconds.ToString("0.00") + " ms/frame  ·  frame " + (Time.smoothDeltaTime * 1000).ToString("0.0") + " ms (" + (1 / Mathf.Max(0.001f, Time.smoothDeltaTime)).ToString("0") + " fps, measured)" +
                             "\nAgents moving " + LivingNPC.All.Count(n => n != null && n.Navigation.Speed > 0.1f) + "  ·  greetings " + manager.Greetings + "  ·  bumps " + manager.Bumps + "\nClock " + SkyState.Clock + " · day " + LivingClock.Day;
        }

        private LivingNPC Centre()
        {
            var camera = Camera.main;
            if (camera == null) return LivingNPC.All.FirstOrDefault();
            LivingNPC best = null; var score = float.MaxValue;
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null) continue;
                var v = camera.WorldToViewportPoint(npc.transform.position + Vector3.up);
                if (v.z <= 0) continue;
                var s = new Vector2(v.x - 0.5f, v.y - 0.5f).sqrMagnitude * 40 + v.z * 0.02f;
                if (s < score) { score = s; best = npc; }
            }
            return best;
        }

        private static string Describe(LivingNPC n)
        {
            var p = n.Profile;
            var next = p != null ? p.Schedule.Next(LivingClock.Hour) : null;
            var memory = NPCRelationshipMemory.Of(n.Id);
            var director = FindFirstObjectByType<NPCEventDirector>();
            var talking = ScenarioManager.Instance != null && ScenarioManager.Instance.Active && ScenarioManager.Instance.Npc == n.Legacy;
            return n.DisplayName + (p != null ? "  ·  " + p.Occupation + " · Band " + p.EnglishBand + " · " + p.Voice.Accent : "") +
                   "\nLocation: " + (n.Place != null ? n.Place.Id : "outside") + (n.Riding ? " (on the tram)" : "") +
                   "\nActivity: " + n.Activity + " — " + n.Step +
                   "\nGoal: " + n.Goal +
                   "\nNext: " + (next != null ? next.ToString() : "—") +
                   "\nHunger " + Mathf.RoundToInt(n.Needs.Hunger * 100) + "%  ·  energy " + Mathf.RoundToInt((1 - n.Needs.Fatigue) * 100) + "%  ·  social " + Mathf.RoundToInt(n.Needs.Social * 100) + "%" +
                   "\nRelationship: " + memory.Relationship + " (" + memory.Encounters + " encounters)" + (memory.Summaries.Count > 0 ? "\n  “" + memory.Summaries[memory.Summaries.Count - 1] + "”" : "") +
                   "\nAnimation: " + n.Animation.Current +
                   "\nNavigation: " + (n.Navigation.HasDestination ? n.Navigation.Destination.ToString("0.0") + (n.Navigation.WaitingAtCrossing ? " · waiting for green" : "") : "—") +
                   "\nTier: " + n.Tier + (n.Hidden ? " (inside)" : "") +
                   "\nEvent: " + (director != null && director.Active != "" ? director.Active : "—") +
                   "\nDialogue: " + (talking ? ScenarioManager.Instance.Machine.Phase.ToString() : n.InConversation ? "chatting" : "—");
        }

        private void Anchors()
        {
            foreach (var m in anchorMarkers) if (m != null) Destroy(m);
            anchorMarkers.Clear();
            if (!showAnchors) return;
            foreach (var place in LivingPlace.All)
                foreach (var spot in place.Spots)
                {
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(marker.GetComponent<Collider>());
                    marker.transform.position = spot.transform.position + Vector3.up * 0.05f;
                    marker.transform.localScale = Vector3.one * 0.18f;
                    marker.GetComponent<Renderer>().material.color = spot.Occupant != null ? new Color(1, 0.4f, 0.3f) : new Color(0.3f, 1, 0.5f);
                    anchorMarkers.Add(marker);
                }
        }
    }
}
