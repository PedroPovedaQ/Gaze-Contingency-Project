using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CodexBuildTools
{
    const string k_MenuPath = "Tools/Codex/Build And Run Android";
    const string k_OutputDir = "Temp/CodexBuilds";
    const string k_OutputApk = "codex_build.apk";

    [MenuItem(k_MenuPath, false, 3000)]
    public static void BuildAndRunAndroidMenu()
    {
        BuildAndRunAndroid();
    }

    // Callable from batchmode: -executeMethod CodexBuildTools.BuildAndRunAndroid
    public static void BuildAndRunAndroid()
    {
        var enabledScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            Debug.LogError("[CodexBuild] No enabled scenes in Build Settings.");
            return;
        }

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android,
                BuildTarget.Android);
            if (!switched)
            {
                Debug.LogError("[CodexBuild] Failed to switch active build target to Android.");
                return;
            }
        }

        Directory.CreateDirectory(k_OutputDir);
        string apkPath = Path.Combine(k_OutputDir, k_OutputApk);

        var options = new BuildPlayerOptions
        {
            scenes = enabledScenes,
            target = BuildTarget.Android,
            locationPathName = apkPath,
            options = BuildOptions.AutoRunPlayer
        };

        Debug.Log($"[CodexBuild] Building Android player to {apkPath} and launching on device...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[CodexBuild] Build succeeded in {summary.totalTime.TotalSeconds:F1}s, size {summary.totalSize / (1024f * 1024f):F1} MB");
        }
        else
        {
            Debug.LogError($"[CodexBuild] Build failed with result: {summary.result}");
        }
    }
}
