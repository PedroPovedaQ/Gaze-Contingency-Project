using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EntryGazeAlignmentChecks
{
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);

    [MenuItem("Tools/Codex/Check Entry Eye Alignment")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        QuickGazeAlignmentChecks.Run();
        var previous = Camera.main;
        var camera = new GameObject("Entry test camera", typeof(Camera));
        var host = new GameObject("Entry test fixture"); host.SetActive(false);
        EntryGazeAlignment entry = null;
        var measurementDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "entry-calibration-check-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(measurementDir);
        try
        {
            if (previous != null) previous.tag = "Untagged";
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 1.7f, 0);
            camera.transform.rotation = Quaternion.Euler(65, 30, 20);
            var game = host.AddComponent<FindObjectGameManager>();
            Check(!game.StartCenteredSession(), "Cannot start trials before entry acceptance");
            var stream = host.AddComponent<SharedGazeStream>();
            Set(stream, "<RecordingPath>k__BackingField", System.IO.Path.Combine(measurementDir, "gaze_observations.jsonl"));
            entry = host.AddComponent<EntryGazeAlignment>();
            var run = entry.Run();
            Check(run.MoveNext() && entry.Outcome == "waiting_for_start", "Explicit Start is required");
            Check(Read<Button>(entry, "m_Start").interactable && !Read<Button>(entry, "m_Ok").interactable,
                "Start available; OK unavailable before checks");
            var anchored = Read<GameObject>(entry, "m_Panel").transform.position;
            Check(Mathf.Abs(anchored.y - 1.42f) < .02f, "Downward head pitch cannot place calibration panel on the floor");
            Check(Mathf.Abs((EntryGazeAlignment.HorizontalFacing(camera.transform.rotation) * Vector3.forward).y) < .001f,
                "Calibration targets use horizontal head facing");
            camera.transform.position = Vector3.zero;
            camera.transform.rotation = Quaternion.Euler(0, 50, 0);
            Check(Read<GameObject>(entry, "m_Panel").transform.position == anchored, "Panel stays still");
            camera.transform.rotation = Quaternion.identity;
            Set(entry, "m_Stream", stream);
            stream.QuickAlignment.BeginGuided(Vector3.zero, Quaternion.identity, 0);
            Call(entry, "CreateDots");
            var targets = Read<Transform[]>(entry, "m_Targets");
            Check(targets.Length == 3 && targets[2] != null, "Three environment dots");
            for (int i = 0; i < 3; i++)
                Check(targets[i].Find("Red fixation center").GetComponent<Image>().color == Color.red, "Each dot has red center");
            Check(Read<TMPro.TextMeshProUGUI>(entry, "m_Countdown") != null, "Countdown displayed at target");
            Call(entry, "UpdateTargetPresentation", 0, 3d, 0f);
            var countdown = Read<TMPro.TextMeshProUGUI>(entry, "m_Countdown");
            var countdownRoot = Read<Transform>(entry, "m_CountdownRoot");
            var centers = Read<Image[]>(entry, "m_Centers");
            Check(countdown.text == "3" && !centers[0].enabled && centers[1].enabled,
                "Countdown replaces only the active red center");
            Check(countdown.rectTransform.sizeDelta.x < 160 && countdown.fontSize < 120,
                "Number fits inside the dot");
            Check(Vector3.Angle(countdownRoot.position - camera.transform.position,
                targets[0].position - camera.transform.position) < .01f, "Number stays centered along the target sight line");
            Call(entry, "UpdateTargetPresentation", 0, 0d, .5f);
            Check(!countdownRoot.gameObject.activeSelf && centers[0].enabled,
                "Sampling reveals the fixation center without a countdown overlay");
            Check(Read<Transform>(entry, "m_AttentionRoot").gameObject.activeSelf,
                "One attention pulse follows countdown completion");
            Capture(entry, camera.GetComponent<Camera>(), "reveal-pulse");
            Set(entry, "m_AttentionUntil", Time.realtimeSinceStartupAsDouble - 1);
            Call(entry, "UpdateTargetPresentation", 0, 0d, .5f);
            Check(!Read<Transform>(entry, "m_AttentionRoot").gameObject.activeSelf,
                "Pulse expires and does not restart during ongoing sampling");
            var fills = Read<Image[]>(entry, "m_Fills");
            float halfwaySize = fills[0].rectTransform.sizeDelta.x, halfwayAlpha = fills[0].color.a;
            Call(entry, "UpdateTargetPresentation", 0, 0d, 1f);
            Check(fills[0].type == Image.Type.Simple && fills[0].rectTransform.sizeDelta.x > halfwaySize &&
                fills[0].color.a > halfwayAlpha && fills[0].transform.localRotation == Quaternion.identity,
                "Progress expands and brightens without a radial spinner");
            Capture(entry, camera.GetComponent<Camera>(), "glow");
            Call(entry, "UpdateTargetPresentation", 0, 3d, 0f);
            Capture(entry, camera.GetComponent<Camera>(), "countdown");
            entry.Accept();
            Check(!entry.Completed && !stream.QuickAlignment.IsActive, "Premature OK cannot pass");
            Call(entry, "Fail", "Synthetic tracking loss");
            Check(!entry.Completed && !stream.QuickAlignment.IsRunning && Read<Button>(entry, "m_Start").interactable,
                "Tracking interruption offers retry and clears candidate");
            Call(entry, "ClearTargets"); Call(entry, "CreateShapes");
            Set(entry, "m_ShowingShapes", true);
            Call(entry, "UpdateTargetPresentation", 0, 3d, 0f);
            var shapes = Read<Renderer[]>(entry, "m_Shapes");
            Check(!shapes[0].enabled && shapes[1].enabled, "Active shape cannot poke through countdown");
            Call(entry, "UpdateTargetPresentation", 0, 0d, 0f);
            Check(shapes[0].enabled && !Read<Transform>(entry, "m_CountdownRoot").gameObject.activeSelf,
                "Shape replaces countdown when sampling begins");
            Capture(entry, camera.GetComponent<Camera>(), "shapes");
            var hitboxes = Read<Collider[]>(entry, "m_Hitboxes");
            Check(hitboxes[0] is BoxCollider && hitboxes[1] is SphereCollider && hitboxes[2] is CapsuleCollider,
                "Three independent shape hitboxes for real gaze highlighting");
            entry.Abort(); Check(!entry.Active && !entry.Completed, "Abort never accepts calibration");
            string path = System.IO.Path.Combine(Application.persistentDataPath, "EyeMeasurements", "entry_alignment_" + Read<string>(entry, "m_EntryId") + ".json");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            entry.ResetForRetry();
            var study=entry.Run(studyVerification:true);
            Check(study.MoveNext() && !Read<Button>(entry,"m_Ok").gameObject.activeSelf && Read<Button>(entry,"m_Skip").gameObject.activeSelf,
                "Simplified preparation hides shape OK but retains Skip");
            Check(Read<TMPro.TextMeshProUGUI>(entry,"m_Text").text.Contains("follow automatically"),
                "Participant knows verification follows automatically");
            entry.Abort();
            path=System.IO.Path.Combine(Application.persistentDataPath,"EyeMeasurements","entry_alignment_"+Read<string>(entry,"m_EntryId")+".json");
            if(System.IO.File.Exists(path))System.IO.File.Delete(path);
            Debug.Log("[EntryGazeAlignmentChecks] PASS: explicit start, countdown/red centers, three-dot mean, three independent shapes, preview-only correction, manual OK and interruption.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(camera);
            if (previous != null) previous.tag = "MainCamera";
            System.IO.Directory.Delete(measurementDir, true);
        }
    }
    // Render the real world-space components for visual inspection, without entering Play mode.
    static void Capture(EntryGazeAlignment entry, Camera camera, string name)
    {
        var root = Read<GameObject>(entry, "m_TargetRoot");
        foreach (var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        camera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .06f); camera.fieldOfView = 30;
        var previous = RenderTexture.active;
        var target = new RenderTexture(1200, 900, 24);
        var pixels = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); pixels.Apply();
            System.IO.Directory.CreateDirectory("Temp/EntryCalibrationPreview");
            System.IO.File.WriteAllBytes("Temp/EntryCalibrationPreview/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
        }
    }

}
