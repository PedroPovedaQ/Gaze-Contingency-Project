using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class StudyFeedbackChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    [MenuItem("Tools/Codex/Check Study Feedback")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        var origin = new Vector3(4, 1, 3);
        Check(TrialReadinessGate.TryReminderDirection(origin, Vector3.right, origin + Vector3.back * 2 + Vector3.up,
            out float yaw, out float pitch) && yaw > 80 && pitch > 20,
            "Cross direction uses gaze-relative yaw and pitch at a translated eye origin.");
        Check(!TrialReadinessGate.TryReminderDirection(origin, Vector3.up, origin + Vector3.forward, out _, out _),
            "Near-vertical gaze cannot invent a horizontal cross direction.");
        CheckVerticalWallDistance();
        foreach (string phrase in DirectionalHintPolicy.AllPhrases())
            Check(Array.IndexOf(VoiceAssistantController.StarterPhrases(), phrase) >= 0 &&
                Array.IndexOf(VoiceAssistantController.PhraseLibrary(), phrase) >= 0,
                "Reminder audio exists in both preparation paths.");

        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        TargetCrumble effect = null;
        var oldRandom = UnityEngine.Random.state;
        try
        {
            target.transform.position = new Vector3(1, 2, 3);
            target.transform.localScale = Vector3.one * .1f;
            Physics.SyncTransforms();
            effect = TargetCrumble.Play(target, null);
            Check(effect != null && !target.GetComponent<Renderer>().enabled && target.activeSelf,
                "Only the original surface is hidden; trial metadata remains alive.");
            Check(effect.GetComponentsInChildren<Collider>().Length == 0 && effect.GetComponentsInChildren<Rigidbody>().Length == 0,
                "Fragments cannot become selection targets or physics bodies.");
            var mesh = effect.GetComponent<MeshFilter>().sharedMesh;
            Check(mesh.vertexCount == 1200 && mesh.triangles.Length == 1200,
                "Exactly 100 independent four-face fragments share one mesh.");
            var before = mesh.vertices;
            typeof(TargetCrumble).GetMethod("UpdateGeometry", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, new object[] { .6f });
            var after = mesh.vertices;
            Check(Vector3.Distance(before[0], after[0]) > .01f, "Fragments move during the crumble.");
            for (int i = 0; i < after.Length; i++)
                Check(!float.IsNaN(after[i].x) && !float.IsInfinity(after[i].y), "Crumble geometry stays finite.");
            float next = UnityEngine.Random.value;
            UnityEngine.Random.state = oldRandom;
            Check(next == UnityEngine.Random.value, "Animation does not perturb stimulus randomization.");
            UnityEngine.Object.DestroyImmediate(target); target = null;
            Check(effect != null && mesh != null, "Final-trial object cleanup does not erase the animation.");
            // Ordinary MonoBehaviours do not receive play-mode OnDestroy in this
            // edit-mode fixture. Exercise the owned-resource lifecycle explicitly.
            typeof(TargetCrumble).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(effect, null);
            UnityEngine.Object.DestroyImmediate(effect.gameObject); effect = null;
            Check(mesh == null, "Animation releases its owned mesh.");
        }
        finally
        {
            UnityEngine.Random.state = oldRandom;
            if (effect != null) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
        Debug.Log("[StudyFeedbackChecks] PASS: gaze-relative cross direction, prepared reminders, 100 animated fragments, no physics/RNG interference and cleanup.");
    }

    static void CheckVerticalWallDistance()
    {
        var host = new GameObject("VerticalWallDistanceFixture"); host.SetActive(false);
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(FindObjectGameManager).GetField("m_SpawnCenter", flags).SetValue(game, new Vector3(3, 1, 2));
            typeof(FindObjectGameManager).GetField("m_SeatedRotation", flags).SetValue(game, Quaternion.Euler(0, 90, 0));
            typeof(FindObjectGameManager).GetField("m_RotationalRadius", flags).SetValue(game, 1.5f);
            typeof(FindObjectGameManager).GetField("m_MaxObjectHeight", flags).SetValue(game, 4f);
            Check(Mathf.Abs(game.SearchPlaneHeight - 4.1f) < .0001f, "Guidance denominator matches the full drawn wall height.");
            Vector3 eye = new Vector3(3, 1, 2), gaze = new Vector3(1, .2f, 0).normalized;
            Check(game.TryVerticalGuidanceError(eye, gaze, 0, 2.3f, out float error) && Mathf.Abs(error - 1) < .0001f,
                "Rotated translated wall uses ray-plane intersection rather than eye-to-target height.");
            Check(DirectionalHintPolicy.VerticalBand(error / game.SearchPlaneHeight) == 1,
                "One metre on a 4.1m wall is a medium correction.");
            Check(game.TryVerticalGuidanceError(eye, gaze * 2, 0, 2.3f, out float scaled) && Mathf.Abs(error - scaled) < .0001f,
                "Ray normalization does not change vertical distance.");
            Check(!game.TryVerticalGuidanceError(eye, Vector3.left, 0, 2.3f, out _), "Backward ray has no target-wall height estimate.");
            Check(!game.TryVerticalGuidanceError(eye, Vector3.up, 0, 2.3f, out _), "Parallel ray has no height estimate.");
            Check(game.TryVerticalGuidanceError(eye, new Vector3(1, -3, 0).normalized, 0, 2.3f, out error) && error > game.SearchPlaneHeight,
                "Gaze below the wall is not clamped into a smaller correction.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
