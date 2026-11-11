using UnityEngine;

namespace ContextSpeak.Cafe
{
    // Built-in GUI text ignores world depth. Shop signs must stay behind walls.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class WorldSign : MonoBehaviour
    {
        private Material material;
        private Font font;
        private void OnEnable()
        {
            font = GetComponent<TextMesh>().font;
            material = new Material(Resources.Load<Shader>("WorldText"));
            GetComponent<Renderer>().sharedMaterial = material;
            UpdateAtlas(font);
            Font.textureRebuilt += UpdateAtlas;
        }
        private void UpdateAtlas(Font updated)
        {
            if (updated == font && material != null) material.mainTexture = font.material.mainTexture;
        }
        private void OnDisable()
        {
            Font.textureRebuilt -= UpdateAtlas;
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }
    }
}
