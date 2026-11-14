using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace ContextSpeak.Cafe.UI
{
    // Explicit bundled fallback also works in WebGL, where OS font discovery is unavailable.
    public static class UiFonts
    {
        private static FontAsset chinese;
        private static PanelTextSettings settings;
        public static PanelTextSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                settings = ScriptableObject.CreateInstance<PanelTextSettings>();
                settings.name = "ContextSpeak text fallback";
                settings.hideFlags = HideFlags.DontSave;
                var source = Resources.Load<Font>("UI/NotoSansCJKsc-Regular");
                if (source != null)
                {
                    chinese = FontAsset.CreateFontAsset(source);
                    chinese.name = "ContextSpeak CJK dynamic SDF";
                    settings.fallbackFontAssets = new System.Collections.Generic.List<FontAsset> { chinese };
                }
                return settings;
            }
        }
    }
}
