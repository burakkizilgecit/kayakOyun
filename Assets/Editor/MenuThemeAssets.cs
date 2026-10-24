using UnityEditor;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Landscapes are shared textures, not sprites or readable CPU copies.
    public sealed class MenuThemeAssets : AssetPostprocessor
    {
        public override uint GetVersion() => 1;
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/MenuThemes/", System.StringComparison.Ordinal)
                || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
