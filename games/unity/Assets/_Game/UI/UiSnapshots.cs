using System;

namespace ContextSpeak.Cafe
{
    // Data shapes for the original café / Life-town UI panels (CafeInterface, LifeInterface). The old ForestCafe and
    // LifeTown scenes and their CafeGame / LifeGame controllers were retired; these stay only for the UI regression checks.
    [Serializable] public class CafeSnapshot
    {
        public string objective, nearby, speaker, line, feedback, transcript, status, recording, phase, stage;
        public bool dialogue, busy, complete, join, practice;
        public int turn, total;
        public float x, z;
    }

    /// JSON command from the browser page (used by SliceGame.OnBrowserCommand).
    [Serializable] public class BrowserCommand { public string action; public string text; }

    [Serializable] public class LifeSnapshot : CafeSnapshot
    {
        public string mode = "life", owner = "LifeRuntime", wallet, bag, menu, repair, place, menuPlan;
        public int step;
        public bool menuOpen;
        public LifeMenuItem[] menuItems = new LifeMenuItem[0];
    }
}
