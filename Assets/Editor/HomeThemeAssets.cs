using UnityEditor;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Import only the new track-specific home frames. Winter and all world/character assets are untouched.
    public sealed class HomeThemeAssets : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/HomeThemes/", System.StringComparison.Ordinal)
                || !assetPath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
