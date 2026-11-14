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
        private bool initialized, interiorVisible;
        [SerializeField] private bool interiorOnly;
        public bool UsesAuthoredInterior => interiorOnly;

        /// Keep the authored room visible from both sides; an inactive shell also stays hidden from LOD updates.
        public void UseAuthoredInterior()
        {
            interiorOnly = true;
            initialized = false;
            ApplyVisibility(true);
        }

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
            IsInside = Plot.Contains(viewer);
            ApplyVisibility(interiorOnly || IsInside);
        }

        private void ApplyVisibility(bool visible)
        {
            if (Exterior == null || initialized && visible == interiorVisible) return;
            initialized = true;
            interiorVisible = visible;
            Exterior.SetActive(!visible);
            foreach (var renderer in Interior)
                if (renderer != null) renderer.enabled = visible;
        }
    }
}
