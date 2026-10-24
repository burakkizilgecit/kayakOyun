using UnityEditor;

namespace SlingRiders.EditorTools
{
    /// Arayüz resimleri (Resources/UI altındaki yeni klasörler) 2'nin kuvvetine yuvarlanmadan, kendi boyutunda içe aktarılır:
    /// varsayılan "ToNearest" ölçek 560x397'lik kartı 512x512'ye çekip en-boy oranını bozuyor, kart resmi kutusunu
    /// doldurmuyordu (2026-10-24, günlük ödül ekranı). Eski temalar kendi işlemcileriyle (HomeThemeAssets vb.) ayarlanır.
    public sealed class UiTextureImport : AssetPostprocessor
    {
        static readonly string[] Folders =
        {
            "Assets/Resources/UI/DailyRewards/", "Assets/Resources/UI/Logo/", "Assets/Resources/UI/Studio/",
            "Assets/Resources/UI/Boost/", "Assets/Resources/UI/MapThumbs/", "Assets/Resources/UI/Kit/",
        };

        void OnPreprocessTexture()
        {
            foreach (var f in Folders)
            {
                if (!assetPath.StartsWith(f)) continue;
                var ti = (TextureImporter)assetImporter;
                ti.npotScale = TextureImporterNPOTScale.None;
                return;
            }
        }
    }
}
