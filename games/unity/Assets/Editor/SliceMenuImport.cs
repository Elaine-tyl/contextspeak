using UnityEditor;
using UnityEngine;

namespace ContextSpeak.Editor
{
    // Menu illustrations are flat UI images; never infer a cubemap from minimal metadata.
    public sealed class SliceMenuImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/SliceMenu/", System.StringComparison.Ordinal))
                return;

            var texture = (TextureImporter)assetImporter;
            texture.textureType = TextureImporterType.Default;
            texture.textureShape = TextureImporterShape.Texture2D;
            texture.alphaIsTransparency = true;
            texture.mipmapEnabled = false;
            texture.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
