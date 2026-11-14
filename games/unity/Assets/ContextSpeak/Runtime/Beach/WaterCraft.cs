using GUI = ContextSpeak.Life.ToolkitGui;
using GUILayout = ContextSpeak.Life.ToolkitLayout;
using GUILayoutUtility = ContextSpeak.Life.ToolkitLayoutUtility;
using Screen = ContextSpeak.Life.ToolkitScreen;
using System;
using ContextSpeak.Cafe;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// A rideable jet ski, kayak or paddleboard: board (short climb-on), W/S throttle, A/D steer,
    /// rides on the wave surface with pitch/roll from the water normal, stays inside its boundary,
    /// R resets to the dock, E gets off near the beach. The camera follows the player on the seat.
    public sealed class WaterCraft : InteractableObject
    {
        public string ActivityId = "jetski";
        public float MaxSpeed = 16, Acceleration = 6, TurnRate = 70, Drag = 0.6f, Draft = 0.15f, SeatHeight = 0.55f;
        public Vector3 BoundaryCentre;
        public float BoundaryRadius = 160;
        public Transform Dock;
        public static WaterCraft Riding { get; private set; }
        public float Speed { get; private set; }
        public event Action<WaterCraft> Boarded, LeftCraft;
        private CafePlayer rider;
        private float yaw, board;
        private Vector3 home;
        private Quaternion homeRotation;

        public override string Label => Riding == this ? "" : "Ride the " + DisplayName;
        public string DisplayName => ActivityId == "jetski" ? "jet ski" : ActivityId;
        public override bool CanInteract => base.CanInteract && Riding == null && LocationFlow.Current is BeachFlow flow && !flow.Busy;
        public override Vector3 Point => transform.position + Vector3.up;

        private void Start()
        {
            home = transform.position;
            homeRotation = transform.rotation;
            yaw = transform.eulerAngles.y;
        }

        public override void Interact(InteractionManager manager)
        {
            if (LocationFlow.Current is BeachFlow flow && !flow.CanRide(this)) return;
            rider = manager.Player;
            rider.Blocked = true;
            Riding = this;
            board = 0;
            Boarded?.Invoke(this);
        }

        public void ResetToDock()
        {
            Speed = 0;
            var target = Dock != null ? Dock : null;
            transform.position = target != null ? target.position : home;
            transform.rotation = target != null ? target.rotation : homeRotation;
            yaw = transform.eulerAngles.y;
        }

        public void GetOff()
        {
            if (Riding != this) return;
            var near = Dock != null ? Dock.position : home;
            rider.Place(near + Vector3.up * 0.2f + (Dock != null ? Dock.right * 1.5f : Vector3.zero));
            rider.Blocked = false;
            rider.Activity = PlayerAnimState.Idle;
            rider.Controls?.Clear();
            Riding = null;
            Speed = 0;
            LeftCraft?.Invoke(this);
            ResetToDock();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var ocean = OceanWaves.Active;
            if (Riding == this && rider != null)
            {
                var keyboard = Keyboard.current;
                var move = rider.Controls != null ? rider.Controls.Movement : Vector2.zero;
                var talking = ScenarioManager.Instance != null && ScenarioManager.Instance.Active;
                if (talking) move = Vector2.zero;
                var throttle = move.y;
                Speed = Mathf.MoveTowards(Speed, throttle > 0 ? MaxSpeed * throttle : throttle < 0 ? -MaxSpeed * 0.25f : 0, Acceleration * dt * (throttle == 0 ? Drag : 1));
                yaw += move.x * TurnRate * dt * Mathf.Clamp01(Mathf.Abs(Speed) / 3 + 0.25f) * Mathf.Sign(Speed == 0 ? 1 : Speed);
                var position = transform.position + Quaternion.Euler(0, yaw, 0) * Vector3.forward * Speed * dt;
                var flat = new Vector3(position.x - BoundaryCentre.x, 0, position.z - BoundaryCentre.z);
                if (flat.magnitude > BoundaryRadius) { position = BoundaryCentre + flat.normalized * BoundaryRadius + Vector3.up * position.y; Speed *= 0.5f; LifeUI.Toast("Buoy line — stay inside the activity area."); }
                var terrain = Terrain.activeTerrain;
                if (terrain != null && terrain.SampleHeight(position) + terrain.transform.position.y > OceanWaves.SeaLevel - 0.35f) { Speed = 0; position = transform.position; }
                transform.position = position;
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame && !talking) { ResetToDock(); LifeUI.Toast("Reset to the start."); }
                if (rider.Controls != null && rider.Controls.Interact && !talking)
                {
                    var nearDock = Vector3.Distance(transform.position, Dock != null ? Dock.position : home) < 25 || Mathf.Abs(Speed) < 0.5f && terrainNear(transform.position);
                    if (nearDock) GetOff(); else LifeUI.Toast("Ride back to the dock to get off (or press R to reset).");
                }
            }
            if (ocean != null)
            {
                var position = transform.position;
                position.y = ocean.Height(position) - Draft + Mathf.Clamp01(Speed / MaxSpeed) * 0.12f;
                transform.position = position;
                var normal = Vector3.Slerp(Vector3.up, ocean.Normal(position), 0.7f);
                var pitch = -Mathf.Clamp01(Speed / MaxSpeed) * 6f;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(pitch, yaw, -Mathf.Clamp((Riding == this && rider != null && rider.Controls != null ? rider.Controls.Movement.x : 0) * Speed * 1.2f, -18, 18));
            }
        }

        private static bool terrainNear(Vector3 position)
        {
            var terrain = Terrain.activeTerrain;
            return terrain != null && terrain.SampleHeight(position) + terrain.transform.position.y > -2.5f;
        }

        private void LateUpdate()
        {
            if (Riding != this || rider == null) return;
            board = Mathf.MoveTowards(board, 1, Time.deltaTime * 2);    // short climb-on
            var seat = transform.position + transform.up * SeatHeight - transform.forward * 0.2f;
            rider.Place(Vector3.Lerp(rider.transform.position, seat - Vector3.up * 0.45f, board));
            rider.Face(transform.forward);
            rider.Activity = PlayerAnimState.Sit;
        }
    }

    /// Ordered buoy gates (jet ski course, kayak loop): timer, checkpoints, completion.
    public sealed class BuoyCourse : MonoBehaviour
    {
        public Transform[] Gates = new Transform[0];
        public string ActivityId = "jetski";
        public float GateRadius = 7;
        public int Next { get; private set; }
        public float Elapsed { get; private set; }
        public bool Running { get; private set; }
        public float Best = -1;
        public event Action<BuoyCourse> Completed;

        public void Begin() { Next = 0; Elapsed = 0; Running = true; Highlight(); }

        private void Update()
        {
            if (!Running) return;
            var craft = WaterCraft.Riding;
            if (craft == null || craft.ActivityId != ActivityId) return;
            Elapsed += Time.deltaTime;
            if (Next < Gates.Length && Gates[Next] != null && Vector3.Distance(Flat(craft.transform.position), Flat(Gates[Next].position)) < GateRadius)
            {
                Next++;
                Highlight();
                if (Next >= Gates.Length)
                {
                    Running = false;
                    if (Best < 0 || Elapsed < Best) Best = Elapsed;
                    Completed?.Invoke(this);
                }
                else LifeUI.Toast("Gate " + Next + "/" + Gates.Length + " ✔");
            }
        }

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0, value.z);

        private void Highlight()
        {
            for (var i = 0; i < Gates.Length; i++)
            {
                if (Gates[i] == null) continue;
                foreach (var renderer in Gates[i].GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", i == Next ? new Color(0.1f, 0.95f, 0.3f) : i < Next ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.35f, 0.1f));
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        private GUIStyle style;
        private void OnEnable() { ToolkitView.Attach(this, DrawToolkit); }

        private void DrawToolkit()
        {
            if (!Running || WaterCraft.Riding == null || WaterCraft.Riding.ActivityId != ActivityId) return;
            style ??= new GUIStyle(GUI.skin.box) { fontSize = 18 };
            GUI.Box(new Rect(Screen.width / 2f - 200, 50, 400, 30), "Gate " + (Next + 1) + "/" + Gates.Length + "  ·  " + Elapsed.ToString("0.0") + " s  ·  " + WaterCraft.Riding.Speed.ToString("0") + " m/s  ·  R reset", style);
        }
    }

    /// A guided ride (boat tour, yacht cruise, banana boat, parasail): you sit on board while the
    /// vehicle follows its route, bobbing on the waves; the activity completes at the end.
    public sealed class GuidedRide : MonoBehaviour
    {
        public string ActivityId = "boattour";
        public Vector3[] Route = new Vector3[0];
        public float Speed = 8, Height = 0;
        public Vector3 SeatOffset = new Vector3(0, 1.2f, 0);
        public bool Running { get; private set; }
        public event Action<GuidedRide> Finished;
        private CafePlayer rider;
        private int target;
        private Vector3 home;
        private Quaternion homeRotation;

        private void Start() { home = transform.position; homeRotation = transform.rotation; }

        public void Begin(CafePlayer player)
        {
            rider = player;
            rider.Blocked = true;
            target = 0;
            Running = true;
        }

        private void Update()
        {
            if (!Running || Route.Length == 0) return;
            var goal = Route[target];
            var delta = new Vector3(goal.x - transform.position.x, 0, goal.z - transform.position.z);
            var step = Speed * Time.deltaTime;
            if (delta.magnitude <= step)
            {
                target++;
                if (target >= Route.Length) { End(); return; }
            }
            else
            {
                transform.position += delta.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 1.5f);
            }
            var ocean = OceanWaves.Active;
            var position = transform.position;
            position.y = Height > 0 ? Height : (ocean != null ? ocean.Height(position) : 0) - 0.3f;
            transform.position = position;
        }

        private void LateUpdate()
        {
            if (!Running || rider == null) return;
            rider.Place(transform.TransformPoint(SeatOffset));
            rider.Face(transform.forward);
            rider.Activity = PlayerAnimState.Sit;
        }

        private void End()
        {
            Running = false;
            rider.Place(home + homeRotation * Vector3.right * 4 + Vector3.up * 1.5f);
            rider.Blocked = false;
            rider.Activity = PlayerAnimState.Idle;
            transform.SetPositionAndRotation(home, homeRotation);
            Finished?.Invoke(this);
        }
    }
}
