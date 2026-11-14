using UnityEngine;

namespace ContextSpeak.City
{
    /// Shared signal timing for every crossroads: east–west and north–south traffic take turns,
    /// with a short all-red gap so pedestrians can cross.
    public static class CitySignals
    {
        public const float Green = 11f, Amber = 2.5f, AllRed = 2f;
        private static float Cycle => 2 * (Green + Amber + AllRed);

        /// 0 = red, 1 = amber, 2 = green for the given axis ("x" = cars driving along x).
        public static int State(bool alongX)
        {
            var t = Time.time % Cycle;
            if (!alongX) t = (t + Cycle / 2) % Cycle;
            if (t < Green) return 2;
            if (t < Green + Amber) return 1;
            return 0;
        }
    }
}
