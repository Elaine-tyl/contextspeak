using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Plays the supplied FBX skeleton clips in place; physics remains on the actor root.
    public sealed class AuthoredMotion : MonoBehaviour
    {
        public string ModelName;
        public Animation Player;
        public CozyAvatar Fallback;
        public AnimationClip Idle, Walk, Run, Vault;
        private Vector3 previous;
        private float previewUntil;
        private string current;
        public string CurrentClip => current ?? "";
        public float ClipTime => Player != null && current != null && Player[current] != null ? Player[current].time : 0;

        private void OnEnable()
        {
            previous = transform.position;
            current = null;
            if (Player == null) return;
            foreach (var clip in new[] { Idle, Walk, Run, Vault })
                if (clip != null) Player.AddClip(clip, clip.name);
            // A remote clone may be created mid-stride. Always restore canonical idle first.
            if (Idle != null) Idle.SampleAnimation(Player.gameObject, 0);
        }

        public bool Preview(string role)
        {
            var clip = role == "Vault" ? Vault : role == "Run" ? Run : role == "Walk" ? Walk : null;
            if (clip == null || Player == null) return false;
            if (Fallback != null) Fallback.enabled = false;
            previewUntil = Time.time + Mathf.Max(clip.length, 2);
            Play(clip);
            return true;
        }

        private void Update()
        {
            var speed = Vector3.Distance(previous, transform.position) / Mathf.Max(Time.deltaTime, 0.001f);
            previous = transform.position;
            if (Time.time < previewUntil || Player == null) return;
            var controller = GetComponentInParent<CafePlayer>();
            if (controller != null) speed = controller.Speed;
            var fallback = speed > 0.15f && Walk == null && Fallback != null;
            if (fallback && current != null && Idle != null) Idle.SampleAnimation(Player.gameObject, 0);
            if (Fallback != null) Fallback.enabled = fallback;
            if (fallback) { Player.enabled = false; current = null; return; }
            var state = controller != null ? controller.State
                : speed > 5 ? PlayerAnimState.Run : speed > 0.15f ? PlayerAnimState.Walk : PlayerAnimState.Idle;
            Play(Locomotion(state, speed));
        }

        public AnimationClip Locomotion(PlayerAnimState state, float speed = 1)
        {
            if ((state == PlayerAnimState.Run || state == PlayerAnimState.Sprint) && Run != null) return Run;
            if ((state == PlayerAnimState.Walk || state == PlayerAnimState.Crouch && speed > 0.15f) && Walk != null) return Walk;
            return Idle;
        }

        private void Play(AnimationClip clip)
        {
            if (clip == null || current == clip.name) return;
            Player.enabled = true;
            Player.CrossFade(clip.name, 0.15f);
            current = clip.name;
        }
    }
}
