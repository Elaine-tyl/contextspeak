using System;
using ContextSpeak.Cafe;
using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    public static class RoomVisualChecks
    {
        public static void Verify()
        {
            var type = typeof(CafeClient).Assembly.GetType("ContextSpeak.Cafe.RemotePlayer");
            if (type == null) throw new Exception("Friend rooms require visual-only remote avatars.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Generated/Meshy/Academy.prefab");
            var root = new GameObject("Remote peer regression");
            try
            {
                var peer = root.AddComponent(type);
                type.GetMethod("SetVisual").Invoke(peer, new object[] { source, "Friend" });
                if (root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                    throw new Exception("Remote peer must use the real prepared protagonist geometry.");
                if (root.GetComponentInChildren<CafePlayer>(true) != null || root.GetComponentInChildren<CharacterController>(true) != null
                    || root.GetComponentInChildren<AudioListener>(true) != null || root.GetComponentInChildren<Camera>(true) != null)
                    throw new Exception("Remote peers cannot take over local input, physics or audio.");
                Debug.Log("Remote player visual checks passed");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
