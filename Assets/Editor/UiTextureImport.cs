using UnityEditor;

namespace SlingRiders.EditorTools
{
    /// Arayüz resimleri (Resources/UI altındaki yeni klasörler) 2'nin kuvvetine yuvarlanmadan, kendi boyutunda içe aktarılır:
    /// varsayılan "ToNearest" ölçek 560x397'lik kartı 512x512'ye çekip en-boy oranını bozuyor, kart resmi kutusunu
    /// doldurmuyordu (2026-10-24, günlük ödül ekranı). Eski temalar kendi işlemcileriyle (HomeThemeAssets vb.) ayarlanır.
    /// Android'de tüm arayüz resimleri ASTC 6x6 (2026-10-26): varsayılan ETC2 RGBA8 (8 bit/piksel) ve sıkıştırmasız
    /// mağaza arka planı APK'yı 133 MB'a çıkarıyordu. Diğer işlemcilerden sonra çalışır (GetPostprocessOrder).
    public sealed class UiTextureImport : AssetPostprocessor
    {
        static readonly string[] Folders =
        {
            "Assets/Resources/UI/DailyRewards/", "Assets/Resources/UI/Logo/", "Assets/Resources/UI/Studio/",
            "Assets/Resources/UI/Boost/", "Assets/Resources/UI/MapThumbs/", "Assets/Resources/UI/Kit/",
        };

        public override uint GetVersion() => 2;
        public override int GetPostprocessOrder() => 100;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            foreach (var f in Folders)
                if (assetPath.StartsWith(f)) ti.npotScale = TextureImporterNPOTScale.None;
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.maxTextureSize = ti.maxTextureSize;
            ti.SetPlatformTextureSettings(android);
        }
    }
}
