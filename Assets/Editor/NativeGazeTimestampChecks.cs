using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;

public static class NativeGazeTimestampChecks
{
    [MenuItem("Tools/Codex/Check Native Gaze Timestamp Diagnostics")]
    public static void Run()
    {
        if (Marshal.SizeOf<NativeGazeRecord>() != 136 || Marshal.SizeOf<NativeGazeStatus>() != 56)
            throw new Exception("Native gaze ABI mismatch.");
        // Nanosecond values must survive JSON as integers, without float rounding.
        var original = new NativeGazeRecord { sequence=9007199254740993UL, runtimePoseTime=9007199254740993L,
            requestedTime=9007199254741993L, result=-1, locationFlags=15, qw=1, offsetQw=1 };
        var roundTrip = JsonUtility.FromJson<NativeGazeRecord>(JsonUtility.ToJson(original));
        if (roundTrip.sequence != original.sequence || roundTrip.runtimePoseTime != original.runtimePoseTime || roundTrip.result != -1)
            throw new Exception("Native timestamps lose precision during serialization.");
        string path = Path.Combine(Path.GetTempPath(), "native-gaze-check-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            using (var writer = new TrialReplayWriter(path))
            {
                if (!writer.AppendJson(JsonUtility.ToJson(original))) throw new Exception(writer.Error);
                writer.Dispose();
                if (writer.Error != null) throw new Exception(writer.Error);
            }
            var saved = JsonUtility.FromJson<NativeGazeRecord>(File.ReadAllText(path));
            if (saved.requestedTime != original.requestedTime) throw new Exception("Native writer changed timestamp.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
        var feature = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)?.GetFeature<NativeGazeTimestampFeature>();
        if (feature == null || !feature.enabled) throw new Exception("Enable native gaze Android feature before testing build.");
        Debug.Log("[NativeGazeTimestampChecks] PASS: ABI, integer nanosecond JSON, asynchronous writer, Android feature enabled.");
    }
}
