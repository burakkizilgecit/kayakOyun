using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SledSurfers.EditorTools
{
    /// Arayüzün PanelSettings varlığını oluşturur. Çalışma anında oluşturulan PanelSettings'in
    /// shader referansları build'de boş kalabildiği için varlık editörde üretilip Resources'ta saklanır.
    [InitializeOnLoad]
    public static class UiAssets
    {
        public const string PanelPath = "Assets/Resources/UI/PanelSettings.asset";
        const string ThemePath = "Assets/Resources/UI/GameTheme.tss";

        static UiAssets()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) == null) Ensure();
            };
        }

        [MenuItem("Sled Surfers/Arayüz Varlıklarını Kur")]
        public static void Ensure()
        {
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                AssetDatabase.ImportAsset(ThemePath);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            }

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            bool created = panel == null;
            if (created) panel = ScriptableObject.CreateInstance<PanelSettings>();

            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;

            if (created) AssetDatabase.CreateAsset(panel, PanelPath);
            else EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            if (theme == null) Debug.LogWarning("[UI] Tema bulunamadı: " + ThemePath);
        }
    }
}
