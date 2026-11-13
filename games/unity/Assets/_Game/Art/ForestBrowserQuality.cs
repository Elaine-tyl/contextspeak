using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Cafe
{
    /// Browser GPUs need a smaller frame budget; desktop keeps the authored forest quality.
    public sealed class ForestBrowserQuality : MonoBehaviour
    {
        public UniversalRenderPipelineAsset Pipeline;
        public ScriptableRendererFeature ContactOcclusion;
        public UniversalAdditionalCameraData Camera;
        private float scale, distance;
        private int cascades, shadowResolution;
        private bool active, occlusion;
        private AntialiasingMode antialiasing;

        private void Start()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer) UseBrowserPreset();
        }

        public void UseBrowserPreset()
        {
            if (active || Pipeline == null || Camera == null) return;
            active = true;
            scale = Pipeline.renderScale;
            distance = Pipeline.shadowDistance;
            cascades = Pipeline.shadowCascadeCount;
            shadowResolution = Pipeline.mainLightShadowmapResolution;
            antialiasing = Camera.antialiasing;
            occlusion = ContactOcclusion != null && ContactOcclusion.isActive;
            Pipeline.renderScale = 0.85f;
            Pipeline.shadowDistance = 28;
            Pipeline.shadowCascadeCount = 2;
            Pipeline.mainLightShadowmapResolution = 1024;
            Camera.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            if (ContactOcclusion != null) ContactOcclusion.SetActive(false);
        }

        private void OnDestroy()
        {
            if (!active || Pipeline == null) return;
            // The same app can return to Life town or the arcade after unloading this scene.
            Pipeline.renderScale = scale;
            Pipeline.shadowDistance = distance;
            Pipeline.shadowCascadeCount = cascades;
            Pipeline.mainLightShadowmapResolution = shadowResolution;
            if (Camera != null) Camera.antialiasing = antialiasing;
            if (ContactOcclusion != null) ContactOcclusion.SetActive(occlusion);
        }
    }
}
