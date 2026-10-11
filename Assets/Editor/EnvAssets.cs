using UnityEditor;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    /// Çevre modelleri (Kenney CC0 paketleri) için içe aktarma ayarları: çalışma anında tek mesh'te
    /// birleştirilebilmeleri için okunabilir olmalılar; animasyon, kamera ve ışık alınmaz.
    public class EnvModelImport : AssetPostprocessor
    {
        // Ayarlar değişince modellerin yeniden içe aktarılması için sürüm artırılır.
        public override uint GetVersion() => 3;

        void OnPreprocessTexture()
        {
            // Meshy karakter dokuları 2K geliyor: telefonda kızaktaki küçük karakter için 1K yeterli.
            var tex = (TextureImporter)assetImporter;
            if (assetPath.Contains("/Resources/Avatars/Portraits/"))
            {
                // Portreler arayüzde: mipmap kenarları koyulaştırmasın, saydam kenar rengi doğru olsun.
                tex.mipmapEnabled = false;
                tex.alphaIsTransparency = true;
                return;
            }
            if (assetPath.Contains("/Resources/UI/Shop/") || assetPath.Contains("/Resources/UI/Offers/"))
            {
                // Mağaza ve fırsat görselleri: arayüzde küçültülerek gösterilir (mipmap + üçlü süzme tırtığı önler),
                // saydam kenar rengi doğru, sıkıştırma yok (boyanmış görselde bantlanma olmasın).
                tex.mipmapEnabled = true;
                tex.filterMode = UnityEngine.FilterMode.Trilinear;
                tex.alphaIsTransparency = true;
                tex.textureCompression = TextureImporterCompression.Uncompressed;
                tex.npotScale = TextureImporterNPOTScale.None;
                tex.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                return;
            }
            if (assetPath.Contains("/Resources/Avatars/Meshy/")) tex.maxTextureSize = 1024;
        }

        void OnPreprocessModel()
        {
            if (assetPath.Contains("/Resources/Avatars/") || assetPath.Contains("/Art/Meshy/"))
            {
                // Karakterler: iskelet + animasyonlar. Legacy olmalı: Generic klipler build'de
                // Animator olmadan örneklenemiyor; oturma pozu ise çalışma anında örneklenir.
                var avatar = (ModelImporter)assetImporter;
                avatar.animationType = ModelImporterAnimationType.Legacy;
                avatar.importAnimation = true;
                avatar.importCameras = false;
                avatar.importLights = false;
                avatar.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                return;
            }
            if (!assetPath.Contains("/Resources/Env/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }
    }

    /// Gökyüzü malzemesini oluşturur. Shader'ın build'e girmesi için malzeme Resources'ta bir varlık olmalı.
    [InitializeOnLoad]
    public static class EnvAssets
    {
        public const string SkyPath = "Assets/Resources/Env/Sky.mat";
        public const string WaterPath = "Assets/Resources/Env/Water.mat";

        static EnvAssets()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<Material>(SkyPath) == null ||
                    AssetDatabase.LoadAssetAtPath<Material>(WaterPath) == null) Ensure();
            };
        }

        [MenuItem("Sled Surfers/Çevre Varlıklarını Kur")]
        public static void Ensure()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(SkyPath) == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader == null) Debug.LogWarning("[ENV] Skybox/Procedural shader bulunamadı");
                else AssetDatabase.CreateAsset(new Material(shader), SkyPath);
            }
            // Su: şeffaf Standard + normal haritası. Anahtar kelime bir varlıkta açık olmalı, yoksa build bu
            // shader varyantını çıkarır ve çalışma anında eklenen dalgacık haritası görünmez.
            if (AssetDatabase.LoadAssetAtPath<Material>(WaterPath) == null)
            {
                var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/GlassMat.mat");
                var water = glass != null ? new Material(glass) : new Material(Shader.Find("Standard"));
                water.EnableKeyword("_NORMALMAP");
                water.SetTexture("_BumpMap", Texture2D.normalTexture);
                water.SetFloat("_Glossiness", 0.96f);
                water.color = new Color(0.3f, 0.42f, 0.5f, 0.72f);
                AssetDatabase.CreateAsset(water, WaterPath);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
