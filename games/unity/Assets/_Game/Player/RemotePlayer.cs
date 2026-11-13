using UnityEngine;

namespace ContextSpeak.Cafe
{
    /// Visual presence only: a peer never owns a local controller, camera or quest.
    public sealed class RemotePlayer : MonoBehaviour
    {
        private GameObject visual;
        private TextMesh label;
        private Vector3 target;
        private Quaternion facing;
        private bool positioned;
        public string Look { get; set; }

        public void SetVisual(GameObject template, string name)
        {
            if (visual != null) Destroy(visual);
            visual = Instantiate(template, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            visual.SetActive(true);
            if (label == null)
            {
                label = new GameObject("Friend name").AddComponent<TextMesh>();
                label.transform.SetParent(transform, false);
                label.transform.localPosition = Vector3.up * 2;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<Renderer>().sharedMaterial = label.font.material;
                label.fontSize = 42;
                label.characterSize = 0.045f;
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = new Color(0.2f, 0.95f, 0.85f);
                label.richText = false;
            }
            label.text = name;
        }

        public void Move(RoomPlayer player)
        {
            target = new Vector3(player.x, player.y, player.z);
            facing = Quaternion.Euler(0, player.yaw, 0);
            // Map travel is a teleport; ordinary motion is interpolated between polls.
            if (!positioned || Vector3.Distance(transform.position, target) > 8)
            {
                transform.SetPositionAndRotation(target, facing);
                positioned = true;
            }
            if (label != null) label.text = player.name + (player.activity == "exploring" ? "" : "\n" + (player.activity == "recording" ? "Speaking" : "Practising") + " · " + player.npc);
        }

        private void Update()
        {
            var blend = 1 - Mathf.Exp(-14 * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, target, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, blend);
            if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
