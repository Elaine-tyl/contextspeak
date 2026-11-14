using UnityEngine;

namespace ContextSpeak.Learning
{
    [CreateAssetMenu(menuName = "ContextSpeak/Learning/Scenario", fileName = "LearningScenario")]
    public sealed class LearningScenarioAsset : ScriptableObject
    {
        public PracticeScenario Definition = new PracticeScenario();
        public int MinBand = 3, MaxBand = 9;
        public int[] ReviewDays = { 1, 3, 7, 14 };

        public PracticeScenario AtBand(int band)
        {
            // A per-session copy keeps generated follow-ups out of authored assets.
            var copy = JsonUtility.FromJson<PracticeScenario>(JsonUtility.ToJson(Definition));
            copy.Band = Mathf.Clamp(band, MinBand, MaxBand);
            return copy;
        }
    }
}
