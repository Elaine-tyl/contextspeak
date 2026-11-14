using UnityEngine;

namespace ContextSpeak.Life
{
    /// Distance-based cost control: far animals think less often, stop animating their bones and stop
    /// casting shadows. (Positions keep updating, so herds are where you expect when you walk up.)
    public sealed class AnimalLODController : MonoBehaviour
    {
        public float AnimateWithin = 90, ShadowsWithin = 60, FullDetailWithin = 35;
        private AnimalController controller;
        private AnimalAnimator animator;
        private Renderer[] renderers;
        private Transform viewer;
        private float timer;
        private bool shadows = true;

        private void Start()
        {
            controller = GetComponent<AnimalController>();
            animator = GetComponent<AnimalAnimator>();
            renderers = GetComponentsInChildren<Renderer>();
            timer = Random.value;
        }

        private void Update()
        {
            if ((timer -= Time.deltaTime) > 0) return;
            timer = 0.5f;
            if (viewer == null) { var camera = Camera.main; if (camera == null) return; viewer = camera.transform; }
            var distance = Vector3.Distance(viewer.position, transform.position);
            if (controller != null) controller.UpdateScale = distance < FullDetailWithin ? 1 : distance < AnimateWithin ? 2 : 6;
            if (animator != null) animator.Active = distance < AnimateWithin;
            var wanted = distance < ShadowsWithin;
            if (wanted == shadows) return;
            shadows = wanted;
            foreach (var item in renderers) if (item != null) item.shadowCastingMode = wanted ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
