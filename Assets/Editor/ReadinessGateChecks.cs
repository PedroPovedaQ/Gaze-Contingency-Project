using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ReadinessGateChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [MenuItem("Tools/Codex/Check Readiness Gate")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        var charge = new StudyReadinessCharge();
        for (int i = 0; i < 12; i++) Check(!charge.Step(true, false, false, 0.1f, 1), "Charge alone cannot bypass announcement.");
        Check(charge.Progress == 1, "Continuous gaze fills charge.");
        Check(!charge.Step(false, false, true, 0.01f, 1) && charge.Progress == 0, "Leaving/tracking loss resets even a full pending charge.");
        charge.Step(true, false, true, 0.1f, 1);
        Check(!charge.Step(true, true, true, 0.01f, 1) && charge.Progress == 0, "Pause resets charge.");
        Check(!charge.Step(true, false, true, 0.5f, 1) && charge.Progress == 0, "Unobserved frame gap cannot complete readiness.");
        for (int i = 0; i < 9; i++) Check(!charge.Step(true, false, true, 0.1f, 1), "No early readiness.");
        Check(charge.Step(true, false, true, 0.11f, 1), "Valid gaze plus announcement completes readiness.");

        var host = new GameObject("ReadinessFixture"); host.SetActive(false);
        var camera = new GameObject("MovingHeadFixture");
        try
        {
            var ui = host.AddComponent<FindObjectUI>();
            var cross = new GameObject("CrossFixture"); cross.transform.SetParent(camera.transform);
            Set(ui, "m_CrossCanvasGO", cross);
            var position = new Vector3(0, 1.5f, 2);
            ui.PositionReadinessCross(position, Quaternion.identity);
            camera.transform.position = new Vector3(4, 3, 5);
            Check(cross.transform.parent == null && cross.transform.position == position, "Cross stays fixed when the head moves.");
            var eye = new Vector3(0, 1.5f, 0);
            Check(ui.IsGazeInsideReadinessCross(eye, new Vector3(0.34f, 0.34f, 2).normalized, 0.15f), "Entire padded cross region accepts gaze, including corners.");
            Check(!ui.IsGazeInsideReadinessCross(eye, new Vector3(0.36f, 0, 2).normalized, 0.15f), "Outside padded region does not charge.");
            Check(!ui.IsGazeInsideReadinessCross(eye, Vector3.back, 0.15f), "Gaze away from the wall does not charge.");
            var game = host.AddComponent<FindObjectGameManager>();
            var gate = host.AddComponent<TrialReadinessGate>();
            Set(game, "m_UI", ui); Set(game, "m_Readiness", gate);
            Set(game, "m_State", FindObjectGameManager.GameState.Playing);
            game.MarkAnnouncementReady(0);
            game.BeginSearch(0);
            Check(!game.SearchActive, "Audio callback cannot bypass gaze gate.");
            Set(gate, "<Completed>k__BackingField", true);
            Set(game, "<AnnouncementReady>k__BackingField", false);
            game.BeginSearch(0);
            Check(!game.SearchActive, "Completed gaze cannot bypass audio gate.");
            var voice = host.AddComponent<VoiceAssistantController>();
            var synth = host.AddComponent<VoiceSynthesizer>();
            Set(voice, "m_GameManager", game); Set(voice, "m_VoiceSynthesizer", synth);
            Set(voice, "m_IntroPlayingOrQueued", true);
            var wait = (System.Collections.IEnumerator)typeof(VoiceAssistantController).GetMethod(
                "WaitForAnnouncementAndResumeTimer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(voice, new object[] { 0, "Blue", "Cube" });
            Check(wait.MoveNext() && wait.MoveNext() && !game.AnnouncementReady,
                "Early object readiness must wait for the queued intro/target, even while no clip is playing.");
            Set(voice, "m_IntroPlayingOrQueued", false);
            Check(!wait.MoveNext() && game.AnnouncementReady && !game.SearchActive,
                "Finished audio marks readiness but does not directly start search.");
            game.MarkAnnouncementReady(0); game.BeginSearch(0);
            Check(game.SearchActive && !cross.activeSelf, "Both gates start search and hide cross.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(camera); }
        Debug.Log("[ReadinessGateChecks] PASS: continuous gaze, resets, audio gate, no timer bypass, fixed world cross, padded whole-cross region and search onset.");
    }
}
