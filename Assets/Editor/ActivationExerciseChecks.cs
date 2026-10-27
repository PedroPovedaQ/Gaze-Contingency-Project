using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class ActivationExerciseChecks
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Read(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);
    static void Check(bool value, string why) { if (!value) throw new Exception(why); }

    [MenuItem("Tools/Codex/Check Activation Exercise")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var previous = Camera.main;
        var camera = new GameObject("Activation test camera", typeof(Camera));
        var host = new GameObject("Activation test fixture"); host.SetActive(false);
        var reports = new List<string>();
        try
        {
            if (previous != null) previous.tag = "Untagged";
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 1.6f, 0);
            camera.transform.rotation = Quaternion.Euler(60, 25, 0);
            var game = host.AddComponent<FindObjectGameManager>();
            var exercise = host.AddComponent<ActivationExercise>();
            Check(exercise.Run().MoveNext() && exercise.Active && !exercise.Completed, "Exercise waits for input");
            reports.Add(ReportPath(exercise));
            Check(exercise.Objects.Count == 3 && game.SpawnedObjects.Count == 0 && game.FoundCount == 0 && !game.SearchActive,
                "Exercise objects do not enter scored trial state");
            Check(host.GetComponent<ControllerRaySelector>().Exercise == exercise, "Exercise owns the real controller input route");
            Check(host.GetComponent<ControllerRaySelector>().ThroughWallSearch, "Exercise uses the study object-only physics mask");
            for (int i = 0; i < 3; i++)
            {
                var obj = exercise.Objects[i];
                Check(Mathf.Abs(obj.transform.position.y - 1.65f) < .001f, "Objects stay in front at eye height despite head pitch");
                Check(obj.layer == 8 && obj.GetComponent<Collider>() != null && obj.GetComponent<SpawnableObjectInfo>() != null,
                    "Same object metadata and physics layer as study selection");
                var grab = obj.GetComponent<XRGrabInteractable>();
                Check(grab.enabled && !grab.allowGazeSelect && grab.selectFilters.count == 1 && !grab.selectFilters.GetAt(0).Process(null, grab),
                    "No eye selection or grabbing");
                var winner = GazeTargetVicinity.ClosestObject(camera.transform.position, obj.transform.position - camera.transform.position,
                    exercise.Objects, GazeTargetVicinity.DefaultPaddingDegrees, out _, out bool ambiguous, out _);
                Check(winner == obj && !ambiguous, "Real guidance resolver identifies intended object " + i);
            }
            exercise.Continue(); Check(!exercise.Completed && exercise.Active, "Continue cannot bypass exercise");
            typeof(ActivationExercise).GetMethod("Complete", Flags).Invoke(exercise, new object[] { "passed" });
            Check(!exercise.Completed && exercise.Active, "Completion core also rejects an incomplete exercise");
            exercise.Capture(exercise.Objects[0]); Check(exercise.Step == 0, "No valid gaze cannot pass via trigger");
            var sample = new GazeObservation(); SharedGazeStream.FillTrialContext(sample, game);
            Check(sample.phase == "activation_exercise" && sample.activationStep == 0 && sample.sourceTrialId == "",
                "Eye rows remain separate from trial outcomes");
            typeof(ActivationExercise).GetMethod("LateUpdate", Flags).Invoke(exercise, null);
            CapturePreview(exercise, camera.GetComponent<Camera>());
            // Edit-mode Application.isFocused is false. Exercise the same receipt/cleanup
            // transition after the runtime focus guard, without weakening that guard.
            typeof(ActivationExercise).GetMethod("Complete", Flags).Invoke(exercise, new object[] { "recalibrate" });
            Check(exercise.RecalibrationRequested && !exercise.Completed && !exercise.Active && exercise.Objects.Count == 0 &&
                host.GetComponent<ControllerRaySelector>().Exercise == null, $"Recalibrate releases objects and controller without passing (focused={Application.isFocused}, active={exercise.Active}, retry={exercise.RecalibrationRequested}, complete={exercise.Completed}, objects={exercise.Objects.Count}, saveError={Read(exercise, "m_SaveError")})");
            Check(File.ReadAllText(reports[0]).Contains("recalibrate"), "Recalibration outcome preserved");
            Check(!host.GetComponent<ControllerRaySelector>().ThroughWallSearch, "Recalibration restores the setup ray policy");
            Check(exercise.Run().MoveNext(), "Fresh exercise can run after recalibration"); reports.Add(ReportPath(exercise));
            var progress = (ActivationCheckProgress)Read(exercise, "m_Progress");
            for (int i = 0; i < 3; i++)
            {
                for (int tick = 0; tick < 10; tick++) progress.Observe(true, i, i + tick * .05);
                Check(progress.Select(i) == "correct", "All three synthetic qualification/selection steps pass");
            }
            typeof(ActivationExercise).GetMethod("Complete", Flags).Invoke(exercise, new object[] { "passed" });
            Check(exercise.Completed && !exercise.Active && game.FoundCount == 0 && !game.SearchActive,
                "Passing never starts or scores a trial by itself");
            Check(File.ReadAllText(reports[1]).Contains("\"outcome\": \"passed\""), "Successful outcome saved before continuation");
            Debug.Log("[ActivationExerciseChecks] PASS: eye-height objects, real gaze winner/selector route, trigger guard, no score changes, telemetry isolation, retry cleanup and saved completion.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(camera);
            if (previous != null) previous.tag = "MainCamera";
            foreach (var path in reports) if (File.Exists(path)) File.Delete(path);
        }
    }
    static string ReportPath(ActivationExercise exercise) => Path.Combine(Application.persistentDataPath,
        "EyeMeasurements", "activation_" + Read(exercise, "m_Id") + ".json");
    static void CapturePreview(ActivationExercise exercise, Camera camera)
    {
        foreach (var obj in exercise.Objects) { obj.layer = 31; obj.SetActive(true); }
        var panel = (GameObject)Read(exercise, "m_Panel");
        panel.SetActive(true);
        foreach (var child in panel.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        camera.transform.rotation = Quaternion.Euler(8, 25, 0);
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f,.045f,.06f); camera.fieldOfView = 45;
        var previous = RenderTexture.active; var target = new RenderTexture(1200,900,24);
        var pixels = new Texture2D(1200,900,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,1200,900),0,0); pixels.Apply();
            Directory.CreateDirectory("Temp/ActivationPreview"); File.WriteAllBytes("Temp/ActivationPreview/exercise.png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
