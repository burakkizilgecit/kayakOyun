using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SledSurfers.EditorTools
{
    /// Sahneyi, malzeme şablonlarını ve oyuncu ayarlarını oluşturur.
    /// Menü: Sled Surfers > Projeyi Kur
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Sled Surfers/Projeyi Kur")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            CreateMaterials();
            CreateScene();

            PlayerSettings.companyName = "SledSurfers";
            PlayerSettings.productName = "Sled Surfers";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SETUP] Proje kuruldu: " + ScenePath);
        }

        /// Komut satırından: kurulum + fizik testleri.
        public static void SetupAndTest()
        {
            Setup();
            SimTests.RunAll();
        }

        static void CreateMaterials()
        {
            var standard = Shader.Find("Standard");
            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/BaseMat.mat") == null)
                AssetDatabase.CreateAsset(new Material(standard) { color = Color.white }, "Assets/Resources/BaseMat.mat");

            if (AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/GlassMat.mat") == null)
            {
                var glass = new Material(standard) { color = new Color(0.85f, 0.95f, 1f, 0.28f) };
                glass.SetFloat("_Mode", 3f);   // Transparent
                glass.SetInt("_SrcBlend", (int)BlendMode.One);
                glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                glass.SetInt("_ZWrite", 0);
                glass.DisableKeyword("_ALPHATEST_ON");
                glass.DisableKeyword("_ALPHABLEND_ON");
                glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                glass.SetFloat("_Glossiness", 0.95f);
                glass.renderQueue = (int)RenderQueue.Transparent;
                AssetDatabase.CreateAsset(glass, "Assets/Resources/GlassMat.mat");
            }
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Game").AddComponent<GameController>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    /// Proje ilk açıldığında boş sahne yerine oyun sahnesini açar.
    [InitializeOnLoad]
    static class OpenMainScene
    {
        static OpenMainScene()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || SessionState.GetBool("SledSurfers.Opened", false)) return;
                SessionState.SetBool("SledSurfers.Opened", true);
                if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && File.Exists("Assets/Scenes/Main.unity"))
                    EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            };
        }
    }
}
