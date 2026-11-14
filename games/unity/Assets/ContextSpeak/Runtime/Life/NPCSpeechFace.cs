using System.Collections.Generic;
using System.Linq;
using ContextSpeak.Cafe;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Talking faces for every NPC in every scene, with no per-scene setup.
    ///  • Lip movement follows the real loudness of the NPC's voice audio (AudioSource.GetOutputData), so the mouth
    ///    opens on syllables and closes in pauses — not a looping flap.
    ///  • Uses whatever the model has: a mouth/jaw blend shape (jawOpen, mouthOpen, viseme_aa …), else a jaw bone,
    ///    else a small head emphasis so rig-less models still visibly "speak".
    ///  • Blinks (blend shape), a light smile while speaking, talk gestures (NPCAnimationController Talk) while it
    ///    speaks, and listening nods while the player records an answer.
    /// The speaker is the character that owns the playing voice source, or the player's conversation partner when
    /// the voice is played centrally (SliceVoice / NpcVoice). ContextSpeak > Checks > NPC Talking Faces reports
    /// which characters in the open scene have a mouth shape, a jaw or only the head fallback.
    [DefaultExecutionOrder(10000)]
    public sealed class NPCSpeechDirector : MonoBehaviour
    {
        private static NPCSpeechDirector instance;
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly float[] buffer = new float[256];
        private float refreshAt;
        private CafePlayer player;
        private CafeRecorder recorder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            instance = new GameObject("NPC speech faces").AddComponent<NPCSpeechDirector>();
            DontDestroyOnLoad(instance.gameObject);
        }

        private void Update()
        {
            if (Time.unscaledTime > refreshAt)
            {
                refreshAt = Time.unscaledTime + 1f;
                sources.Clear();
                sources.AddRange(FindObjectsByType<AudioSource>(FindObjectsSortMode.None));
                player = FindFirstObjectByType<CafePlayer>();
                recorder = FindFirstObjectByType<CafeRecorder>();
            }
            var partner = player != null ? player.ConversationPartner : null;
            foreach (var source in sources)
            {
                if (source == null || !source.isPlaying || source.loop || source.clip == null || source.clip.length > 60) continue;
                if (!IsVoice(source)) continue;
                var speaker = Speaker(source, partner);
                if (speaker == null) continue;
                source.GetOutputData(buffer, 0);
                double sum = 0;
                for (var i = 0; i < buffer.Length; i++) sum += buffer[i] * buffer[i];
                NPCSpeechFace.For(speaker).Feed(Mathf.Sqrt((float)(sum / buffer.Length)));
            }
            if (partner != null && recorder != null && recorder.State == "recording")
                NPCSpeechFace.For(partner).Listen();
        }

        /// Gated voice components report whether their line (not e.g. the player's own replay) is playing.
        private static bool IsVoice(AudioSource source)
        {
            var slice = source.GetComponent<ContextSpeak.Slice.SliceVoice>();
            if (slice != null) return slice.Playing;
            var npc = source.GetComponent<NpcVoice>();
            if (npc != null) return npc.Playing;
            return source.spatialBlend > 0.5f && source.GetComponentInParent<Animator>() != null; // 3D voice on a character
        }

        private Transform Speaker(AudioSource source, Transform partner)
        {
            var anim = source.GetComponentInParent<NPCAnimationController>();
            Transform root = anim != null ? anim.transform : null;
            if (root == null)
            {
                var animator = source.GetComponentInParent<Animator>();
                if (animator != null && source.GetComponent<ContextSpeak.Slice.SliceVoice>() == null && source.GetComponent<NpcVoice>() == null) root = animator.transform;
            }
            if (root == null) root = partner;
            if (root == null || (player != null && root.IsChildOf(player.transform))) return null;
            return root;
        }
    }

    [DefaultExecutionOrder(10001)]
    public sealed class NPCSpeechFace : MonoBehaviour
    {
        public static readonly string[] OpenNames = { "jawopen", "jaw_open", "mouthopen", "mouth_open", "viseme_aa", "v_aa", "mth_open", "mouth_a", "vrc.v_aa", "aa", "a" };
        public static readonly string[] SmileNames = { "mouthsmile", "mouth_smile", "smile", "happy", "joy" };
        public static readonly string[] BlinkNames = { "eyeblink", "eye_blink", "blink", "eyesclosed", "eyes_closed", "eyeclose" };

        public float Mouth { get; private set; }
        public string Method { get; private set; } = "";
        private readonly List<(SkinnedMeshRenderer renderer, int index)> open = new List<(SkinnedMeshRenderer, int)>();
        private readonly List<(SkinnedMeshRenderer renderer, int index)> smile = new List<(SkinnedMeshRenderer, int)>();
        private readonly List<(SkinnedMeshRenderer renderer, int index)> blink = new List<(SkinnedMeshRenderer, int)>();
        private Transform jaw, head;
        private NPCAnimationController rig;
        private float target, lastVoice = -10, lastListen = -10, talkTime, blinkAt, blinkPhase = -1, nodPhase, smileWeight;
        private bool gestured;
        private NPCAction before;

        public static NPCSpeechFace For(Transform character)
        {
            var face = character.GetComponent<NPCSpeechFace>();
            if (face == null) { face = character.gameObject.AddComponent<NPCSpeechFace>(); face.Scan(); }
            return face;
        }

        /// Exact match first, then "contains", per priority name — so "viseme_aa" beats an unrelated "a…" shape.
        public static int Pick(Mesh mesh, string[] names)
        {
            if (mesh == null) return -1;
            var shapes = Enumerable.Range(0, mesh.blendShapeCount).Select(i => (i, n: mesh.GetBlendShapeName(i).ToLowerInvariant())).ToArray();
            foreach (var name in names)
            {
                foreach (var s in shapes) if (s.n == name || s.n.EndsWith("." + name) || s.n.EndsWith("_" + name)) return s.i;
                if (name.Length > 2) foreach (var s in shapes) if (s.n.Contains(name)) return s.i;
            }
            return -1;
        }

        public void Scan()
        {
            open.Clear(); smile.Clear(); blink.Clear();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh; if (m == null || m.blendShapeCount == 0) continue;
                var o = Pick(m, OpenNames); if (o >= 0) open.Add((r, o));
                var s = Pick(m, SmileNames); if (s >= 0) smile.Add((r, s));
                var b = Pick(m, BlinkNames); if (b >= 0) blink.Add((r, b));
            }
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (jaw == null && n.Contains("jaw") && !n.Contains("end")) jaw = t;
                if (head == null && (n == "head" || n.EndsWith(":head") || n.EndsWith("_head") || n.EndsWith(".head"))) head = t;
            }
            rig = GetComponent<NPCAnimationController>();
            if (head == null && rig != null) head = rig.Bone("Head");
            Method = open.Count > 0 ? "mouth blend shape" : jaw != null ? "jaw bone" : head != null ? "head emphasis (no mouth/jaw on this model)" : "none (static mesh)";
            blinkAt = Time.time + Random.Range(1.5f, 4f);
        }

        /// level: RMS of the voice output this frame.
        public void Feed(float level)
        {
            lastVoice = Time.time;
            target = Mathf.Max(target, Mathf.Clamp01((level - 0.01f) * 9f));
        }

        public void Listen() { lastListen = Time.time; }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            var speaking = Time.time - lastVoice < 0.15f;
            // Fast attack, slower release: syllables open the mouth, pauses close it.
            Mouth = Mathf.MoveTowards(Mouth, speaking ? target : 0, dt * (target > Mouth ? 18f : 9f));
            target = Mathf.MoveTowards(target, 0, dt * 12f);
            talkTime = speaking ? talkTime + dt : 0;
            smileWeight = Mathf.MoveTowards(smileWeight, speaking ? 0.25f : 0.1f, dt);

            foreach (var (r, i) in open) if (r != null) r.SetBlendShapeWeight(i, Mouth * 85f);
            foreach (var (r, i) in smile) if (r != null) r.SetBlendShapeWeight(i, smileWeight * 100f);
            if (open.Count == 0 && jaw != null) jaw.rotation = Quaternion.AngleAxis(Mouth * 14f, transform.right) * jaw.rotation;

            if (head != null)
            {
                var listening = Time.time - lastListen < 0.3f;
                if (listening) nodPhase += dt * 1.6f;
                var nod = listening ? Mathf.Max(0, Mathf.Sin(nodPhase * Mathf.PI)) * 4f : 0f;
                var emphasis = speaking ? Mouth * (open.Count == 0 && jaw == null ? 5f : 1.5f) : 0f;
                var sway = speaking ? Mathf.Sin(Time.time * 1.7f) * 1.2f : 0f;
                head.rotation = Quaternion.AngleAxis(nod + emphasis, transform.right) * Quaternion.AngleAxis(sway, Vector3.up) * head.rotation;
            }

            // Blinking every 2–5 s (0.15 s close/open).
            if (blink.Count > 0)
            {
                if (blinkPhase < 0 && Time.time > blinkAt) blinkPhase = 0;
                var w = 0f;
                if (blinkPhase >= 0)
                {
                    blinkPhase += dt / 0.15f;
                    w = blinkPhase < 1 ? Mathf.Sin(blinkPhase * Mathf.PI) : 0;
                    if (blinkPhase >= 1) { blinkPhase = -1; blinkAt = Time.time + Random.Range(2f, 5f); }
                }
                foreach (var (r, i) in blink) if (r != null) r.SetBlendShapeWeight(i, w * 100f);
            }

            // Gestures: talk while speaking, listen pose while the player answers; never override a task animation.
            if (rig == null) return;
            if (speaking && talkTime > 0.4f && !gestured && (rig.Action == NPCAction.None || rig.Action == NPCAction.Listen))
            {
                before = rig.Action; gestured = true; rig.Play(NPCAction.Talk);
            }
            else if (!speaking && gestured && Time.time - lastVoice > 0.8f)
            {
                gestured = false;
                if (rig.Action == NPCAction.Talk) rig.Play(Time.time - lastListen < 0.3f ? NPCAction.Listen : before);
            }
            else if (!speaking && !gestured && Time.time - lastListen < 0.3f && rig.Action == NPCAction.None)
            {
                before = NPCAction.None; gestured = true; rig.Play(NPCAction.Listen);
            }
            else if (gestured && !speaking && Time.time - lastListen > 1f && rig.Action == NPCAction.Listen)
            {
                gestured = false; rig.Play(before);
            }
        }
    }
}
