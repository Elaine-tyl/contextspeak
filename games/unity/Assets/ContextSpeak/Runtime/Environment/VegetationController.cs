using UnityEngine;

namespace ContextSpeak.Life
{
    /// Root of a scattered vegetation set (created by ContextSpeak > Environment > Setup Realistic World).
    /// Trees, bushes, flowers and grass tufts carry LODGroups (distance culling, scaled by the
    /// quality preset's LOD bias); this component thins the small plants to the grass-density
    /// setting and drops them during the night far from the camera to save draw calls.
    public sealed class VegetationController : MonoBehaviour
    {
        public enum Layer { Tree, Bush, Flower, Grass }

        [System.Serializable]
        public struct Item
        {
            public GameObject Plant;
            public Layer Type;
            [Range(0, 1)] public float Keep;     // random 0…1; drawn while Keep < density
        }

        public Item[] Items = new Item[0];
        [Tooltip("Grass and flowers beyond this distance are hidden (also scaled by density).")]
        public float DetailDistance = 70;

        private float appliedDensity = -1;
        private float timer;

        private void OnEnable() { appliedDensity = -1; Refresh(); }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 0.5f;
            Refresh();
        }

        public void Refresh()
        {
            var density = Mathf.Clamp01(EnvironmentTuning.GrassDensity);
            var camera = Camera.main;
            var reach = DetailDistance * Mathf.Lerp(0.6f, 1.2f, density);
            var reachSq = reach * reach;
            var focus = camera != null ? camera.transform.position : Vector3.zero;
            var densityChanged = !Mathf.Approximately(density, appliedDensity);
            appliedDensity = density;
            for (var i = 0; i < Items.Length; i++)
            {
                var item = Items[i];
                if (item.Plant == null) continue;
                var small = item.Type == Layer.Grass || item.Type == Layer.Flower;
                var visible = !small || item.Keep < density && (camera == null || (item.Plant.transform.position - focus).sqrMagnitude < reachSq);
                if (item.Plant.activeSelf != visible && (densityChanged || small)) item.Plant.SetActive(visible);
            }
        }
    }
}
