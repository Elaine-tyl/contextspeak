using ContextSpeak.Life;
using UnityEngine;
namespace ContextSpeak.Proactive
{
    /// Reuses the supplied rig/action adapter. Procedural fallbacks are reported by that adapter.
    public sealed class NPCReactionAnimator : MonoBehaviour
    {
        private NPCAnimationController rig;
        private bool ownsAnimation;
        private bool reacting;
        private GameObject prop;
        public bool HasHand => rig != null && rig.Bone("RightHand") != null;
        private void Awake()
        {
            rig = GetComponent<NPCAnimationController>();
            ownsAnimation = rig == null;
            if (ownsAnimation) rig = gameObject.AddComponent<NPCAnimationController>();
        }
        public void React(string action, Transform player)
        {
            reacting = true;
            rig.LookAt(player.position + Vector3.up * 1.5f);
            rig.Play(action == "collision" ? NPCAction.Shrug : action == "greeting" ? NPCAction.Wave : NPCAction.Talk);
        }
        public void CarryAndDrop(string kind)
        {
            if (prop != null || string.IsNullOrEmpty(kind)) return;
            prop = Props.Make(kind);
            prop.name = "Proactive dropped " + kind;
            var hand = rig.Bone("RightHand");
            if (hand != null) { prop.transform.SetParent(hand, false); prop.transform.localPosition = Vector3.zero; }
            else prop.transform.position = transform.position + transform.forward * .45f + Vector3.up * .8f;
            prop.transform.SetParent(null, true);
            // Small props only, with a bounded fall and no impulse applied to either character.
            prop.transform.position = transform.position + transform.right * .5f + Vector3.up * .6f;
            var collider = prop.GetComponent<Collider>();
            if (collider == null) collider = prop.AddComponent<BoxCollider>();
            var box = collider as BoxCollider;
            if (box != null) box.size = kind == "map" ? new Vector3(.28f, .025f, .22f) : new Vector3(.12f, .15f, .12f);
            var body = prop.AddComponent<Rigidbody>();
            body.mass = .15f; body.linearDamping = 1; body.angularDamping = 2;
            body.maxLinearVelocity = 2; body.maxAngularVelocity = 3;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var own = GetComponentsInChildren<Collider>();
            foreach (var item in own) if (item != collider) Physics.IgnoreCollision(collider, item);
            var player = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            if (player != null) foreach (var item in player.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, item);
        }
        public void PickUp() { rig.Play(NPCAction.PickUp); if (prop != null) Destroy(prop); }
        public void Release() { reacting = false; rig.Play(NPCAction.None); rig.LookAt(null); if (prop != null) Destroy(prop); }
        private void Update()
        {
            if (!ownsAnimation || !reacting) return;
            rig.Tick(Time.deltaTime, 0, true);
        }
        private void LateUpdate() { if (ownsAnimation && reacting) rig.Late(Time.deltaTime, true); }
        private void OnDisable() { Release(); }
    }
}
