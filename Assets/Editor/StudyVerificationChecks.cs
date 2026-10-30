using System;
using UnityEditor;
using UnityEngine;

/// <summary>Offline regression checks; these do not substitute for Vive measurements.</summary>
public static class StudyVerificationChecks
{
    [MenuItem("Tools/Codex/Check Study Integration")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before verification.");
        ScheduleChecks.Run();
        GazeQualityChecks.Run();
        GazeWallFilterChecks.Run();
        ControllerSelectionChecks.Run();
        ReadinessGateChecks.Run();
        StudyFeedbackChecks.Run();
        GuidanceBoundaryChecks.Run();
        VoiceChoiceChecks.Run();
        TrialReplayChecks.Run();
        Debug.Log("[StudyVerificationChecks] PASS: offline study integration. Headset accuracy, rate, audio and interaction validation remain required.");
    }
}
