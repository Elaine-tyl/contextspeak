using System.Linq;
using UnityEngine;

namespace ContextSpeak.Life
{
    /// Fewer people on the streets at night and in the rain (they go indoors), more on sunny days.
    public sealed class SkyCrowd : MonoBehaviour
    {
        public GameObject[] People = new GameObject[0];
        private float timer;

        private void Start()
        {
            if (People.Length == 0)
                People = FindObjectsByType<ContextSpeak.City.CityPedestrian>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(person => person.gameObject).ToArray();
            // Deterministic shuffle so whole streets don't empty at once.
            People = People.OrderBy(person => (person.name.GetHashCode() ^ (int)(person.transform.position.x * 31)) & 0x7fffffff).ToArray();
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0) return;
            timer = 2f;
            var wanted = Mathf.RoundToInt(People.Length * SkyState.Crowd);
            for (var i = 0; i < People.Length; i++)
            {
                if (People[i] == null) continue;
                var keep = i < wanted;
                if (People[i].activeSelf != keep) People[i].SetActive(keep);
            }
        }
    }
}
