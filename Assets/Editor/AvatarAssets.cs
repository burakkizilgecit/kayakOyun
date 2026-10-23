using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    /// Meshy animasyonları (Assets/Art/Meshy/<Ad>_<Klip>.fbx) karakterin mesh'iyle birlikte geliyor.
    /// Build'e yalnızca klip girsin diye her biri Resources/Avatars/Meshy/<Ad>_<Klip>.anim olarak çıkarılır.
    public class AvatarClipImport : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Any(p => p.StartsWith(AvatarAssets.SourceDir))) EditorApplication.delayCall += AvatarAssets.Ensure;
        }
    }

    public static class AvatarAssets
    {
        public const string SourceDir = "Assets/Art/Meshy";
        public const string TargetDir = "Assets/Resources/Avatars/Meshy";

        [MenuItem("Sling Riders/Karakter Kliplerini Çıkar")]
        public static void Ensure()
        {
            if (!Directory.Exists(SourceDir)) return;
            Directory.CreateDirectory(TargetDir);
            foreach (var fbx in Directory.GetFiles(SourceDir, "*.fbx"))
            {
                string path = fbx.Replace('\\', '/');
                string target = TargetDir + "/" + Path.GetFileNameWithoutExtension(path) + ".anim";
                // Kaynak klipten daha yeni bir .anim varsa yeniden yazılmaz.
                if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(path)) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                                        .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null)
                {
                    Debug.LogWarning("[AVATAR] Klip yok: " + path);
                    continue;
                }
                var copy = Object.Instantiate(clip);
                copy.name = Path.GetFileNameWithoutExtension(path);
                copy.legacy = true;
                AssetDatabase.DeleteAsset(target);
                AssetDatabase.CreateAsset(copy, target);
                Debug.Log("[AVATAR] Klip çıkarıldı: " + target + " (" + clip.length.ToString("F2") + " s)");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
