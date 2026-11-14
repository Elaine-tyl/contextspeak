using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Finds the nearest usable object, shows its prompt and runs it on E. Also owns sitting and
    /// standing, because a seated player's E/WASD means "stand up".
    public sealed class InteractionManager : MonoBehaviour
    {
        public static InteractionManager Instance { get; private set; }
        public CafePlayer Player;
        public InteractableObject Focus { get; private set; }
        public SeatController Seat { get; private set; }
        /// Set by modal panels (menus, dialogue, payment) so E and movement don't leak into the world.
        public bool Modal { get; set; }
        private InteractableObject[] all = new InteractableObject[0];
        private float rescan;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (Player == null) return;
            if (Time.time >= rescan) { all = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None); rescan = Time.time + 1f; }
            var controls = Player.Controls;
            if (Modal) { Focus = null; return; }
            var origin = Player.transform.position + Vector3.up;
            // While seated you can still eat, drink or call staff; anything else (E with nothing in
            // reach, or moving) stands you up.
            Focus = all.Where(item => item != null && item.CanInteract && Vector3.Distance(item.Point, origin) < item.Range
                    && (Seat == null || !(item is SeatController) && !(item is PortalDoor)))
                .OrderBy(item => Vector3.Distance(item.Point, origin)).FirstOrDefault();
            if (controls == null) return;
            if (Seat != null)
            {
                var moving = controls.Movement.sqrMagnitude > 0.2f;
                if (controls.Interact && Focus != null) Focus.Interact(this);
                else if ((controls.Interact || moving) && Seat.CanStand) Stand();
                return;
            }
            if (Focus != null && controls.Interact) Focus.Interact(this);
        }

        public void Sit(SeatController seat)
        {
            if (seat == null || seat.Occupied) return;
            Seat = seat;
            seat.Occupied = true;
            Player.Route.Clear();
            Player.Place(seat.SitPoint);
            Player.Face(seat.transform.forward);
            Player.Activity = PlayerAnimState.Sit;
            seat.Seated?.Invoke();
        }

        public void Stand()
        {
            if (Seat == null) return;
            var seat = Seat;
            Seat = null;
            seat.Occupied = false;
            Player.Activity = PlayerAnimState.Idle;
            Player.Place(seat.StandPoint);
            seat.Stood?.Invoke();
        }
    }
}
