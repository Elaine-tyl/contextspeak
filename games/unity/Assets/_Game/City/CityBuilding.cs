using UnityEngine;

namespace ContextSpeak.City
{
    /// An enterable building. While the player is inside, the roof and upper storey hide so the
    /// third-person camera can see the room, and the interior lights come on.
    public sealed class CityBuilding : MonoBehaviour
    {
        public string Title = "Building";
        public string TitleZh = "";
        public Bounds Inside;
        public GameObject[] HideWhenInside = new GameObject[0];
        public Light[] Lights = new Light[0];

        /// The building the player is in, or null on the street.
        public static CityBuilding Current { get; private set; }

        private Transform player;
        private bool inside;

        private void Start()
        {
            var found = FindFirstObjectByType<ContextSpeak.Cafe.CafePlayer>();
            if (found != null) player = found.transform;
            Apply(false);
        }

        private void Update()
        {
            if (player == null) return;
            var now = Inside.Contains(player.position + Vector3.up * 0.5f);
            if (now == inside) return;
            Apply(now);
        }

        private void Apply(bool now)
        {
            inside = now;
            if (now) Current = this;
            else if (Current == this) Current = null;
            foreach (var item in HideWhenInside)
                if (item != null) item.SetActive(!now);
            foreach (var light in Lights)
                if (light != null) light.enabled = true;
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
