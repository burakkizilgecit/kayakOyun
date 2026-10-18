using UnityEditor;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    /// Import only the illustrated home-menu surfaces. No environment, model or shared UI assets change.
    public sealed class WinterHomeAssets : AssetPostprocessor
    {
        public override uint GetVersion() => 2;

        void OnPreprocessTexture()
        {
            const string prefix = "Assets/Resources/UI/HomeWinter/";
            bool navigation = assetPath == prefix + "navigation-icons.png";
            if (assetPath != prefix + "track-plaque.png"
                && assetPath != prefix + "play-button.png"
                && assetPath != prefix + "upgrade-panel.png"
                && !navigation) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = navigation ? TextureImporterType.Default : TextureImporterType.Sprite;
            importer.spriteImportMode = navigation ? SpriteImportMode.None : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.maxTextureSize = navigation ? 1024 : 2048;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            // Slice borders are applied by WinterHomeArt once the final painted edge widths are known.
        }
    }
}
