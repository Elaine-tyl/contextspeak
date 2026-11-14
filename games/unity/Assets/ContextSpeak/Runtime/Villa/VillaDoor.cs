using UnityEngine;
using UnityEngine.InputSystem;

namespace ContextSpeak.Life
{
    /// A villa door the player opens/closes with E and locks/unlocks with L (lockable doors: bathrooms,
    /// front door). Residents of the villa (LivingNPCs whose home is the villa) open it for themselves —
    /// they have keys — and it returns to the player's chosen state behind them. The leaf's pivot is the
    /// hinge (Blender export), so a swing is a rotation of this transform about Y. Roll-up (garage) and
    /// sliding (gate) doors move instead. State is saved in VillaState.
    public sealed class VillaDoor : InteractableObject
    {
        public enum Motion { Swing, RollUp, Slide }
        public string Id = "door";
        public Motion Mode = Motion.Swing;
        [Tooltip("Swing angle in degrees about +Y (sign = direction).")] public float Angle = 90;
        [Tooltip("Roll-up: rise (m). Slide: local offset when open.")] public float Rise = 2.5f;
        public Vector3 SlideOffset = new Vector3(6, 0, 0);
        public float Speed = 1.4f;
        public bool Lockable;
        public bool Locked;
        public bool ResidentsOpen = true;
        [Tooltip("Optional: the villa id whose residents may pass (LivingNPC.Profile.Home).")] public string Home = "villa";

        public bool IsOpen { get; private set; }
        public float Openness => t;
        private float t;
        private Quaternion closedRotation;
        private Vector3 closedPosition, centre;
        private bool started;

        private void Start()
        {
            closedRotation = transform.localRotation;
            closedPosition = transform.localPosition;
            var renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) { var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); centre = b.center; }
            else centre = transform.position;
            if (!VillaState.Loaded) VillaState.Load();
            var saved = VillaState.Door(Id);
            IsOpen = saved.open; Locked = Lockable && saved.locked;
            t = IsOpen ? 1 : 0;
            Apply();
            started = true;
        }

        public override Vector3 Point => started ? centre : transform.position;
        public override string Label => Locked ? "Unlock the " + Prompt : (IsOpen ? "Close the " : "Open the ") + Prompt;

        public override void Interact(InteractionManager manager)
        {
            if (Locked) { SetLocked(false); return; }
            SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (open && Locked) { ActionHud.Instance?.Notice("door-locked", "L", "The " + Prompt + " is locked"); return; }
            IsOpen = open;
            var s = VillaState.Door(Id); s.open = open; VillaState.Touch();
        }

        public void SetLocked(bool locked)
        {
            if (!Lockable) return;
            if (locked && IsOpen) SetOpen(false);
            Locked = locked;
            var s = VillaState.Door(Id); s.locked = locked; VillaState.Touch();
            ActionHud.Instance?.Notice("door-lock", "L", (locked ? "Locked" : "Unlocked") + " the " + Prompt);
        }

        private bool ResidentNear()
        {
            if (!ResidentsOpen) return false;
            var r = Mode == Motion.Swing ? 1.8f : 4f;
            foreach (var npc in LivingNPC.All)
            {
                if (npc == null || npc.Hidden || !npc.isActiveAndEnabled) continue;
                if (!string.IsNullOrEmpty(Home) && npc.Profile != null && npc.Profile.Home != Home) continue;
                if ((npc.transform.position - centre).sqrMagnitude < r * r) return true;
            }
            foreach (var pet in VillaPet.All) if (pet != null && (pet.transform.position - centre).sqrMagnitude < 1.4f * 1.4f) return true;
            return false;
        }

        private void Update()
        {
            var manager = InteractionManager.Instance;
            if (Lockable && manager != null && manager.Focus == this && !IsOpen)
            {
                ActionHud.Instance?.Prompt("door-lock-" + Id, "L", Locked ? "Unlock" : "Lock");
                if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame) SetLocked(!Locked);
            }
            var wanted = IsOpen || ResidentNear() ? 1f : 0f;
            if (Mathf.Approximately(wanted, t)) return;
            t = Mathf.MoveTowards(t, wanted, Speed * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            var e = Mathf.SmoothStep(0, 1, t);
            switch (Mode)
            {
                case Motion.Swing: transform.localRotation = closedRotation * Quaternion.Euler(0, Angle * e, 0); break;
                case Motion.RollUp: transform.localPosition = closedPosition + Vector3.up * Rise * e; break;
                case Motion.Slide: transform.localPosition = closedPosition + SlideOffset * e; break;
            }
        }
    }
}
