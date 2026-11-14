using System;
using System.Linq;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Plays the supplied FBX skeleton clips in place; physics remains on the actor root.
    [DefaultExecutionOrder(100)]
    public sealed class AuthoredMotion : MonoBehaviour
    {
        public string ModelName;
        public Animation Player;
        public CozyAvatar Fallback;
        public AnimationClip Idle, Walk, Run, Vault, Jump, Crouch;
        [Range(0, 1)] public float JumpApex = .5f;
        public AnimationClip[] Extras = Array.Empty<AnimationClip>();
        /// Set by gameplay systems (swimming) to play a clip instead of locomotion; OverrideTime >= 0 holds that pose.
        [NonSerialized] public AnimationClip Override;
        [NonSerialized] public float OverrideSpeed = 1, OverrideFade = 0.25f, OverrideTime = -1;

        public AnimationClip Find(string role) => role == "Idle" ? Idle : role == "Walk" ? Walk
            : role == "Run" ? Run : role == "Vault" ? Vault : role == "Jump" ? Jump : role == "Crouch" ? Crouch
            : Extras.FirstOrDefault(clip => clip != null && clip.name == ModelName + "-" + role);
        private Vector3 previous;
        private float previewUntil;
        private string current;
        private bool samplingJump;
        public string CurrentClip => current ?? "";
        public float ClipTime => Player != null && current != null && Player[current] != null ? Player[current].time : 0;

        private void OnEnable()
        {
            previous = transform.position;
            current = null;
            if (Player == null) return;
            foreach (var clip in new[] { Idle, Walk, Run, Vault, Jump, Crouch }.Concat(Extras))
                if (clip != null) Player.AddClip(clip, clip.name);
            // A remote clone may be created mid-stride. Always restore canonical idle first.
            if (Idle != null) Idle.SampleAnimation(Player.gameObject, 0);
        }

        public bool Preview(string role)
        {
            var clip = Find(role);
            if (clip == null || Player == null) return false;
            if (Fallback != null) Fallback.enabled = false;
            previewUntil = Time.time + Mathf.Max(clip.length, 2);
            current = null; // Replaying the same preview must start it from the beginning.
            Player.Stop();
            Play(clip);
            return true;
        }

        private void Update()
        {
            var speed = Vector3.Distance(previous, transform.position) / Mathf.Max(Time.deltaTime, 0.001f);
            previous = transform.position;
            if (Time.time < previewUntil || Player == null) return;
            if (Override != null) { PlayOverride(); return; }
            var controller = GetComponentInParent<CafePlayer>();
            if (controller != null) speed = controller.Speed;
            var fallback = speed > 0.15f && Walk == null && Fallback != null;
            if (fallback && current != null && Idle != null) Idle.SampleAnimation(Player.gameObject, 0);
            if (Fallback != null) Fallback.enabled = fallback;
            if (fallback) { Player.enabled = false; current = null; return; }
            var state = controller != null ? controller.State
                : speed > 5 ? PlayerAnimState.Run : speed > 0.15f ? PlayerAnimState.Walk : PlayerAnimState.Idle;
            if (controller != null && Jump != null && (state == PlayerAnimState.Jump || state == PlayerAnimState.Fall || state == PlayerAnimState.Land))
            {
                SampleAirborne(state == PlayerAnimState.Land ? 1 : controller.AirProgress);
                return;
            }
            Play(Locomotion(state, speed));
        }

        private void PlayOverride()
        {
            if (Fallback != null) Fallback.enabled = false;
            if (OverrideTime >= 0)
            {
                Player.Stop();
                Player.enabled = false;
                Override.SampleAnimation(Player.gameObject, Mathf.Clamp(OverrideTime, 0, Override.length));
                current = Override.name;
                samplingJump = true;
                return;
            }
            if (Player.GetClip(Override.name) == null) Player.AddClip(Override, Override.name);
            if (samplingJump) { current = null; samplingJump = false; }
            if (current != Override.name) { Player.enabled = true; Player.CrossFade(Override.name, OverrideFade); current = Override.name; }
            var playing = Player[Override.name];
            if (playing != null) playing.speed = OverrideSpeed;
        }

        public AnimationClip Locomotion(PlayerAnimState state, float speed = 1)
        {
            if ((state == PlayerAnimState.Jump || state == PlayerAnimState.Fall) && Jump != null) return Jump;
            if (state == PlayerAnimState.Crouch && Crouch != null) return Crouch;
            if ((state == PlayerAnimState.Run || state == PlayerAnimState.Sprint) && Run != null) return Run;
            if ((state == PlayerAnimState.Walk || state == PlayerAnimState.Crouch && speed > 0.15f) && Walk != null) return Walk;
            return Idle;
        }

        public void SampleAirborne(float progress)
        {
            if (Jump == null || Player == null) return;
            // Clamp a long fall at the landing pose instead of looping a leap or translating the rig twice.
            progress = Mathf.Clamp01(progress);
            var phase = progress <= .5f ? Mathf.Lerp(0, JumpApex, progress * 2)
                : Mathf.Lerp(JumpApex, 1, (progress - .5f) * 2);
            Player.Stop();
            Player.enabled = false;
            Jump.SampleAnimation(Player.gameObject, phase * Jump.length);
            current = Jump.name;
            samplingJump = true;
        }

        private void Play(AnimationClip clip)
        {
            if (samplingJump) { current = null; samplingJump = false; }
            if (clip == null || current == clip.name) return;
            Player.enabled = true;
            Player.CrossFade(clip.name, 0.15f);
            current = clip.name;
        }
    }
}
