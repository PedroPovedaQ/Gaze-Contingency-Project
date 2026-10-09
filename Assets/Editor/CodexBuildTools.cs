using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CodexBuildTools
{
    const string k_MenuPath = "Tools/Codex/Build And Run Android";
    const string k_OutputDir = "Builds/CodexAndroid";
    const string k_OutputApk = "codex_build.apk";

    [MenuItem(k_MenuPath, false, 3000)]
    public static void BuildAndRunAndroidMenu()
    {
        BuildAndRunAndroid();
    }

    // Callable from batchmode: -executeMethod CodexBuildTools.BuildAndRunAndroid
    public static void BuildAndRunAndroid()
    {
        BuildAndroid(BuildOptions.None, true);
    }

    // Offline packaging check when no headset is attached. No install or launch.
    [MenuItem("Tools/Codex/Build Android APK", false, 3002)]
    public static void BuildAndroid()
    {
        BuildAndroid(BuildOptions.None, false);
    }

    [MenuItem("Tools/Codex/Clean Build And Run Android", false, 3001)]
    public static void CleanBuildAndRunAndroid()
    {
        BuildAndroid(BuildOptions.CleanBuildCache, true);
    }

    static void BuildAndroid(BuildOptions additionalOptions, bool launchOnDevice)
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            throw new BuildFailedException("[CodexBuild] No enabled scenes in Build Settings.");
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
            if (!switched)
            {
                throw new BuildFailedException("[CodexBuild] Failed to switch active build target to Android.");
            }
        }

        Directory.CreateDirectory(k_OutputDir);
        string apkPath = Path.Combine(k_OutputDir, k_OutputApk);

        // OpenXR's composition-layer validator reads the selected group, even
        // when BuildPlayer targets Android. Keep it aligned with this build so
        // a selected desktop profile cannot incorrectly validate desktop settings.
        EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.Android;

        var options = new BuildPlayerOptions
        {
            scenes = enabledScenes,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            locationPathName = apkPath,
            options = additionalOptions | (launchOnDevice ? BuildOptions.AutoRunPlayer : BuildOptions.None)
        };

        Debug.Log($"[CodexBuild] Building Android player to {apkPath}; launch on device: {launchOnDevice}.");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[CodexBuild] Build succeeded in {summary.totalTime.TotalSeconds:F1}s, size {summary.totalSize / (1024f * 1024f):F1} MB");
        }
        else
        {
            throw new BuildFailedException($"[CodexBuild] Build failed with result: {summary.result}; errors: {summary.totalErrors}");
        }
    }
}
