using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    public static class BuildTools
    {
        /// Komut satırından: otomatik görsel test için Windows sürümü derler.
        public static void BuildWindowsTest()
        {
            UiAssets.Ensure();
            EnvAssets.Ensure();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/WindowsTest/SledSurfers.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            });
            Debug.Log("[BUILD] " + report.summary.result + " hata=" + report.summary.totalErrors);
        }
    }
}
