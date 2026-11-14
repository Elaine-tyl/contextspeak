using System;
using System.Collections;
using UnityEngine;

namespace ContextSpeak.Cafe
{
    public enum PlayerLook { Girl, Boy }

    /// Switch visuals only: the controller, camera, hand anchor and quests stay on their existing roots.
    public sealed class PlayerAppearance : MonoBehaviour
    {
        public const string Preference = "contextspeak.player-look";
        public GameObject Girl, Boy;
        public bool PreviewOnly;
        private float baseHeight, baseRadius;
        private Coroutine preview;
        public PlayerLook Selected { get; private set; }

        private void Start() { if (!PreviewOnly) Restore(); }

        public bool PreviewMotion(string model, string role)
        {
            var target = model == "Academy" ? Girl : model == "Daydream" ? Boy : null;
            var motion = target != null ? target.GetComponentInChildren<AuthoredMotion>(true) : null;
            if (motion == null) return false;
            if (preview != null) StopCoroutine(preview);
            Girl.SetActive(target == Girl);
            Boy.SetActive(target == Boy);
            if (!motion.Preview(role)) { Restore(); return false; }
            var clip = motion.Find(role);
            if (Application.isPlaying) preview = StartCoroutine(RestoreAfter(Mathf.Max(clip.length, 2) + 0.1f));
            else Restore();
            return true;
        }

        private IEnumerator RestoreAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            preview = null;
            Restore();
        }

        public void Restore()
        {
            ApplyProfile(CharacterProfiles.Draft());
        }

        public bool Choose(PlayerLook look)
        {
            if (!Enum.IsDefined(typeof(PlayerLook), look)) return false;
            var profile = CharacterProfiles.Draft();
            profile.Look = look;
            if (CharacterProfiles.Exists && !CharacterProfiles.TrySave(profile, out _)) return false;
            ApplyProfile(profile);
            PlayerPrefs.SetInt(Preference, (int)look);
            PlayerPrefs.Save();
            return true;
        }

        public void ApplyProfile(CharacterProfile source)
        {
            if (preview != null) { StopCoroutine(preview); preview = null; }
            var profile = source.Copy();
            profile.Normalize();
            Selected = profile.Look;
            if (Girl != null) Girl.SetActive(Selected == PlayerLook.Girl);
            if (Boy != null) Boy.SetActive(Selected == PlayerLook.Boy);
            var scale = new Vector3(profile.Build, profile.Height, profile.Build);
            if (Girl != null) Girl.transform.localScale = scale;
            if (Boy != null) Boy.transform.localScale = scale;
            if (PreviewOnly) return;
            var player = GetComponentInParent<CafePlayer>();
            var collider = player != null ? player.GetComponent<CharacterController>() : null;
            if (collider == null) return;
            if (baseHeight == 0) { baseHeight = player.StandingHeight; baseRadius = collider.radius; }
            player.StandingHeight = baseHeight * profile.Height;
            player.CrouchHeight = player.StandingHeight * (1.1f / 1.7f);
            collider.height = player.Crouching ? player.CrouchHeight : player.StandingHeight;
            collider.center = Vector3.up * collider.height * 0.5f;
            collider.radius = baseRadius * profile.Build;
        }
    }
}
