using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Only visual skins switch. Physics, NPCs, voice and quest objects stay active.
    public sealed class BuildingSkin : MonoBehaviour
    {
        public string SourceId, Kind, Scene;
        public GameObject Exterior;
        public Bounds Plot;
        public Renderer[] Interior = new Renderer[0];
        public bool IsInside { get; private set; }
        private CafePlayer player;
        private bool initialized;

        private void Start()
        {
            if (Kind == "room") player = FindFirstObjectByType<CafePlayer>();
        }

        private void LateUpdate()
        {
            if (player != null) Refresh(player.transform.position);
        }

        public void Refresh(Vector3 viewer)
        {
            if (Kind != "room" || Exterior == null) return;
            var inside = Plot.Contains(viewer);
            if (initialized && inside == IsInside) return;
            initialized = true;
            IsInside = inside;
            Exterior.SetActive(!inside);
            foreach (var renderer in Interior)
                if (renderer != null) renderer.enabled = inside;
        }
    }
}
