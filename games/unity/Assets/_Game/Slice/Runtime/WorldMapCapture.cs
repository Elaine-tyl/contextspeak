using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ContextSpeak.Slice
{
    // Reuses two small targets. Render requests are throttled, not additional full-size cameras every frame.
    public sealed class WorldMapCapture : MonoBehaviour
    {
        public RenderTexture MapTexture { get; private set; }
        public RenderTexture WorldTexture { get; private set; }
        public bool CapturingPhone;
        public event Action Captured;
        private Camera map, world;
        private float nextCapture;
        private readonly UniversalRenderPipeline.SingleCameraRequest mapRequest = new UniversalRenderPipeline.SingleCameraRequest();
        private readonly UniversalRenderPipeline.SingleCameraRequest worldRequest = new UniversalRenderPipeline.SingleCameraRequest();
        private void Awake()
        {
            MapTexture = Target(256, 256, "ContextSpeak real minimap");
            WorldTexture = Target(480, 270, "ContextSpeak phone glass background");
            var cameraObject = new GameObject("ContextSpeak map camera");
            cameraObject.transform.SetParent(transform, false);
            map = cameraObject.AddComponent<Camera>(); map.enabled = false;
            map.transform.position = new Vector3(0, 110, 6); map.transform.rotation = Quaternion.Euler(90, 0, 0);
            map.orthographic = true; map.orthographicSize = 66; map.nearClipPlane = .1f; map.farClipPlane = 200;
            map.clearFlags = CameraClearFlags.SolidColor; map.backgroundColor = new Color(.83f, .86f, .78f);
            var data = map.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false; data.renderShadows = false;
            mapRequest.destination = MapTexture; worldRequest.destination = WorldTexture;
        }
        private static RenderTexture Target(int width, int height, string name)
        {
            var result = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = name };
            result.Create(); return result;
        }
        private void LateUpdate()
        {
            if (Time.unscaledTime < nextCapture) return;
            nextCapture = Time.unscaledTime + .5f;
            if (world == null) world = Camera.main;
            if (world == null) return;
            map.cullingMask = world.cullingMask;
            if (RenderPipeline.SupportsRenderRequest(map, mapRequest)) RenderPipeline.SubmitRenderRequest(map, mapRequest);
            if (CapturingPhone && RenderPipeline.SupportsRenderRequest(world, worldRequest)) RenderPipeline.SubmitRenderRequest(world, worldRequest);
            Captured?.Invoke();
        }
        private void OnDestroy()
        {
            if (MapTexture != null) { MapTexture.Release(); Destroy(MapTexture); }
            if (WorldTexture != null) { WorldTexture.Release(); Destroy(WorldTexture); }
        }
    }
}
