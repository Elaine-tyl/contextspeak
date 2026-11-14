using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// Swimming against a flat sea level (independent of the visual waves): WASD swim (Shift faster),
    /// Space up, C dive. Breath only drains with your head under water; a snorkel lets you breathe
    /// face-down at the surface, a dive tank gives several minutes. Running out pushes you up.
    public sealed class Swimming : MonoBehaviour
    {
        public static Swimming Instance { get; private set; }
        public CafePlayer Player;
        public float SurfaceFeet = -1.45f;           // feet height while floating (head just above water)
        public float SwimSpeed = 1.5f, FastSpeed = 2.6f, DiveSpeed = 1.1f;
        public float BreathSeconds = 35;
        public enum Gear { None, Snorkel, Tank }
        public Gear Equipment = Gear.None;
        public bool InWater { get; private set; }
        public bool HeadUnder { get; private set; }
        public float Breath { get; private set; } = 1;
        public float Depth => InWater ? Mathf.Max(0, OceanWaves.SeaLevel - (Player.transform.position.y + 1.6f)) : 0;
        public float TankSeconds = 300;
        private float tank = 1;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private float Seabed(Vector3 position)
        {
            var terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : -50;
        }

        private void LateUpdate()
        {
            if (Player == null) return;
            var modern = SwimmingController.Handles(Player) ? Player.GetComponent<SwimmingController>() : null;
            if (modern != null) { InWater = modern.IsSwimming; HeadUnder = modern.HeadUnder; Breath = modern.Breath; return; }
            var position = Player.transform.position;
            var bed = Seabed(position);
            var deepEnough = bed < SurfaceFeet - 0.1f;
            if (!InWater && deepEnough && position.y < SurfaceFeet + 0.3f && !Player.Blocked) Enter();
            if (InWater && (!deepEnough || bed > position.y - 0.05f) && !HeadUnder) { Leave(); return; }
            if (!InWater) return;
            if (ScenarioManager.Instance != null && ScenarioManager.Instance.Active) return;
            var keyboard = Keyboard.current;
            var controls = Player.Controls;
            var move = controls != null ? controls.Movement : Vector2.zero;
            var camera = Camera.main != null ? Camera.main.transform : Player.transform;
            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);
            var speed = keyboard != null && keyboard.leftShiftKey.isPressed ? FastSpeed : SwimSpeed;
            var velocity = (forward * move.y + right * move.x) * speed;
            var vertical = 0f;
            if (keyboard != null && keyboard.cKey.isPressed && Equipment != Gear.None) vertical -= DiveSpeed;
            if (keyboard != null && keyboard.spaceKey.isPressed) vertical += DiveSpeed * 1.4f;
            if (vertical == 0 && position.y < SurfaceFeet) vertical = 0.35f;     // gentle buoyancy back up
            var next = position + (velocity + Vector3.up * vertical) * Time.deltaTime;
            next.y = Mathf.Clamp(next.y, Seabed(next) + 0.05f, SurfaceFeet);
            if (velocity.sqrMagnitude > 0.01f) Player.Face(velocity);
            Player.Place(next);
            Player.Activity = velocity.sqrMagnitude > 0.05f ? PlayerAnimState.Walk : PlayerAnimState.Crouch;

            HeadUnder = next.y + 1.6f < OceanWaves.SeaLevel - 0.05f;
            var breathing = !HeadUnder || (Equipment == Gear.Tank && tank > 0);
            if (Equipment == Gear.Tank && HeadUnder) tank = Mathf.Max(0, tank - Time.deltaTime / TankSeconds);
            Breath = breathing ? Mathf.Min(1, Breath + Time.deltaTime / 3) : Mathf.Max(0, Breath - Time.deltaTime / BreathSeconds);
            if (Breath <= 0) { Player.Place(new Vector3(next.x, SurfaceFeet, next.z)); Breath = 0.3f; LifeUI.Toast("Out of breath — you swim back to the surface."); }
        }

        private void Enter()
        {
            InWater = true;
            Breath = 1;
            Player.Blocked = true;
            LifeUI.Toast("Swimming · WASD swim · Shift faster" + (Equipment != Gear.None ? " · C dive · Space surface" : " · rent a snorkel to dive"));
        }

        private void Leave()
        {
            InWater = false;
            HeadUnder = false;
            Player.Blocked = false;
            Player.Activity = PlayerAnimState.Idle;
            Player.Controls?.Clear();
        }

        public void Equip(Gear gear) { Equipment = gear; tank = 1; }

        private GUIStyle style;
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); }

        private void DrawToolkit()
        {
            if (!InWater) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 16 };
            var label = (Equipment == Gear.Tank ? "Air " + Mathf.RoundToInt(tank * 100) + "%  ·  " : "") + "Breath";
            GUI.Box(new Rect(Screen.width - 270, Screen.height - 70, 250, 26), label + "  ·  depth " + Depth.ToString("0.0") + " m", style);
            var bar = new Rect(Screen.width - 270, Screen.height - 40, 250 * Breath, 12);
            var previous = GUI.color;
            GUI.color = Breath > 0.3f ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.4f, 0.3f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }

    /// Underwater look: when the camera dips below the water surface the fog turns dense and
    /// turquoise and the view tints blue; it fades back smoothly at the surface.
    public sealed class UnderwaterView : MonoBehaviour
    {
        public Color Water = new Color(0.05f, 0.42f, 0.5f);
        public float Density = 0.09f;
        private float blend;
        private Color skyFog;
        private float skyDensity;
        public static bool Active { get; private set; }

        private void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var ocean = OceanWaves.Active;
            var surface = ocean != null ? ocean.Height(camera.transform.position) : OceanWaves.SeaLevel;
            var under = camera.transform.position.y < surface - 0.05f;
            if (!under && blend <= 0) { Active = false; SkySystem.FogLocked = false; skyFog = RenderSettings.fogColor; skyDensity = RenderSettings.fogDensity; return; }
            blend = Mathf.MoveTowards(blend, under ? 1 : 0, Time.deltaTime * 3);
            Active = blend > 0.5f;
            SkySystem.FogLocked = blend > 0;
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(skyFog, Water * Mathf.Lerp(0.3f, 1f, SkyState.Daylight), blend);
            RenderSettings.fogDensity = Mathf.Lerp(skyDensity, Density, blend);
        }

        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); }

        private void DrawToolkit()
        {
            if (blend <= 0) return;
            var previous = GUI.color;
            GUI.color = new Color(0.1f, 0.45f, 0.55f, 0.28f * blend);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
