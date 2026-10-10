using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SledSurfers.EditorTools
{
    public static class BuildTools
    {
        [MenuItem("Sled Surfers/Build Android Test APK")]
        public static void BuildAndroidTest()
        {
            UiAssets.Ensure();
            EnvAssets.Ensure();
            AvatarAssets.Ensure();
            System.IO.Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.sledsurfers.sledrunner");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/Android/SledSurfers-test.apk",
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            });
            Debug.Log("[ANDROID BUILD] " + report.summary.result + " errors=" + report.summary.totalErrors);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Android APK build failed: " + report.summary.result);
        }

        /// Komut satırından: otomatik görsel test için Windows sürümü derler.
        public static void BuildWindowsTest()
        {
            UiAssets.Ensure();
            EnvAssets.Ensure();
            AvatarAssets.Ensure();
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
