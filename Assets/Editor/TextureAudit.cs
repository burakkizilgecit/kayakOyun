using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SlingRiders.EditorTools
{
    /// Arayüz dokularının Android'deki biçimi ve tahmini boyutu (APK boyutu denetimi, 2026-10-26).
    public static class TextureAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder("[AUDIT] Android doku biçimleri\n");
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetDatabase.LoadAssetAtPath<Texture2D>(path) is Texture2D tex)) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                var fmt = ti.GetAutomaticFormat("Android");
                var ps = ti.GetPlatformTextureSettings("Android");
                sb.AppendLine(path + "\t" + tex.width + "x" + tex.height + "\t" + fmt + "\tmip=" + ti.mipmapEnabled
                              + "\tmax=" + ti.maxTextureSize + "\tcomp=" + ti.textureCompression + "\tovr=" + ps.overridden + "\tnpot=" + ti.npotScale);
            }
            Debug.Log(sb.ToString());
        }
    }
}
