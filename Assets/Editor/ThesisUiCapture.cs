using System;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// GPU batch-mode illustrations of existing runtime UI, without Play mode or services.
/// Run with -executeMethod ThesisUiCapture.Run and THESIS_UI_CAPTURE_OUTPUT set.
/// Fixtures bypass recording/transfer gates; they are never participant evidence.
/// </summary>
public static class ThesisUiCapture
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    const int Width = 2560, Height = 1440;
    static readonly StringBuilder Receipt = new StringBuilder();
    static string output;
    static Camera camera;
    static T Read<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Fields).Invoke(obj, args);

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlaying)
            throw new InvalidOperationException("Use a dedicated GPU batch editor, outside Play mode.");
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            throw new InvalidOperationException("GPU required; omit -nographics.");
        output = Environment.GetEnvironmentVariable("THESIS_UI_CAPTURE_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Set THESIS_UI_CAPTURE_OUTPUT.");
        Directory.CreateDirectory(output);
        Receipt.AppendLine($"Unity {Application.unityVersion}; GPU {SystemInfo.graphicsDeviceName}; {Width}x{Height}");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/GazeContingencyStudyScene.unity", OpenSceneMode.Single);
        var sceneCamera = Camera.main;
        if (sceneCamera == null) throw new InvalidOperationException("Study camera missing.");
        camera = new GameObject("Thesis render camera", typeof(Camera)).GetComponent<Camera>();
        camera.CopyFrom(sceneCamera);
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == camera.gameObject) continue;
            // Keep the authored environment/lighting; suppress unrelated onboarding and XR visuals.
            if (root.name != "Environment" && root.name != "Lighting" && root.name != "Directional Light") root.SetActive(false);
        }
        camera.tag = "MainCamera";
        camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
        camera.enabled = true; // Camera.main ignores disabled cameras, even in edit mode.
        camera.fieldOfView = 35;
        camera.aspect = (float)Width / Height;
        camera.cullingMask = ~0;
        Receipt.AppendLine($"Scene camera background retained: {camera.clearFlags}, {camera.backgroundColor}; authored Environment remains visible. Passthrough is unavailable in editor.");
        AddDevelopmentCaption();
        // Inactive fixture hosts never run Awake/Start/Update, open recorders or initialize providers.
        var host = new GameObject("Synthetic development capture fixture");
        host.SetActive(false);
        var entry = host.AddComponent<EntryGazeAlignment>();
        var stream = host.AddComponent<SharedGazeStream>();
        try
        {
            var routine = entry.Run(studyVerification: true);
            routine.MoveNext();
            Capture("01-calibration-instructions-standard.png");
            Set(entry, "<Active>k__BackingField", false); // Reset must not save an outcome.
            entry.ResetForRetry();
            routine = entry.Run(studyVerification: false);
            routine.MoveNext();
            Capture("02-calibration-instructions-shape-path.png");
            Set(entry, "m_Stream", stream);
            stream.QuickAlignment.BeginGuided(camera.transform.position, Quaternion.identity, 0);
            Call(entry, "CreateDots");
            Read<Button>(entry, "m_Start").interactable = false;
            Read<Button>(entry, "m_Start").GetComponentInChildren<TMP_Text>().text = "Retry calibration";
            // Exact runtime strings; assertions below fail if the runtime wording drifts.
            const string dotPrompt = "Look at the red center. Keep your head still through the countdown and recording.";
            RequireSource("Assets/EntryGazeAlignment.cs", dotPrompt);
            foreach (int number in new[] { 3, 2, 1 })
            {
                Read<TMP_Text>(entry, "m_Text").text = "<b>Calibration dot 1 of 3</b>\n" + dotPrompt + "\nGet ready...";
                Capture($"03-calibration-countdown-{number}.png",
                    () => Call(entry, "UpdateTargetPresentation", 0, (double)number, 0f));
            }
            Read<TMP_Text>(entry, "m_Text").text = "<b>Calibration dot 1 of 3</b>\n" + dotPrompt + "\nRecording eye samples...";
            Set(entry, "m_WasCountingDown", false);
            Set(entry, "m_AttentionUntil", 0d);
            Call(entry, "UpdateTargetPresentation", 0, 0d, .8f);
            Capture("04-calibration-fixation-collection.png");
            Call(entry, "ClearTargets");
            Call(entry, "CreateShapes");
            Set(entry, "m_ShowingShapes", true);
            Read<Transform>(entry, "m_CountdownRoot").gameObject.SetActive(false);
            var shapes = Read<Renderer[]>(entry, "m_Shapes");
            for (int i = 0; i < shapes.Length; i++)
                shapes[i].sharedMaterial.color = i == 1 ? new Color(1, .788f, .016f) : new Color(.2f, .5f, .9f);
            Read<Button>(entry, "m_Start").interactable = true;
            Read<Button>(entry, "m_Ok").interactable = true;
            Read<TMP_Text>(entry, "m_Text").text = RuntimeText("Assets/EntryGazeAlignment.cs", "<b>Check the three shapes</b>");
            Capture("05-calibration-shapes-optional.png");
            Call(entry, "ClearTargets");
            Read<GameObject>(entry, "m_Panel").SetActive(false);

            var voice = host.AddComponent<VoiceModeSelector>();
            Call(voice, "BuildPanel");
            try
            {
                VoiceState(voice, "ConfirmDesktop", AssignmentText("A"));
                Capture("06-headset-session-confirmation-A.png");
                VoiceState(voice, "ConfirmDesktop", AssignmentText("B"));
                Capture("07-headset-session-confirmation-B.png");
                string selected = RuntimeText("Assets/VoiceModeSelector.cs", "<b>Check {SessionConfig.NeutralVoiceName}’s audio</b>")
                    .Replace("{SessionConfig.NeutralVoiceName}", "Development voice");
                VoiceState(voice, "CheckingNeutral", selected);
                Capture("08-headset-selected-audio-check.png");
                VoiceState(voice, "CheckingSelf", RuntimeText("Assets/VoiceModeSelector.cs", "<b>Check your self-similar audio</b>"));
                Capture("09-headset-self-similar-audio-check.png");
            }
            finally { Object.DestroyImmediate(Read<GameObject>(voice, "m_CanvasGO")); }
            File.WriteAllText(Path.Combine(output, "render-receipt.txt"), Receipt.ToString());
            Debug.Log("[ThesisUiCapture] PASS: rendered runtime UI with synthetic development fixtures; no Play mode or services.");
        }
        finally
        {
            // Clearing the fixture reference avoids persistence via calibration Abort/OnDisable.
            Set(entry, "<Active>k__BackingField", false);
            Set(entry, "m_Stream", null);
            Object.DestroyImmediate(host);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static void VoiceState(VoiceModeSelector voice, string phase, string text)
    {
        var field = typeof(VoiceModeSelector).GetField("m_Phase", Fields);
        field.SetValue(voice, Enum.Parse(field.FieldType, phase));
        Call(voice, "SetText", text);
        Call(voice, "RefreshChoices");
    }

    static string AssignmentText(string configuration)
    {
        var assignment = new ParticipantSessionAssignment { participantCode = "P003", configuration = configuration, development = true };
        const string path = "Assets/VoiceModeSelector.cs";
        string order = assignment.NeutralFirst ? "Selected Voice first, then Self-Similar Voice." :
            "Self-Similar Voice first, then Selected Voice.";
        RequireSource(path, order);
        return RuntimeText(path, "<b>Confirm your session</b>")
            .Replace("{assignment.participantCode}", assignment.participantCode)
            .Replace("{assignment.configuration}", assignment.configuration) + order +
            RuntimeText(path, "\\nConfirm the code with the researcher before continuing.");
    }

    // Retrieve display-only strings directly from source, without calling methods that
    // transfer tickets, play audio or write participant readiness records.
    static string RuntimeText(string path, string prefix)
    {
        string source = File.ReadAllText(path);
        int start = source.IndexOf("\"" + prefix, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException("Runtime text changed: " + prefix);
        start++;
        int end = source.IndexOf('"', start);
        return source.Substring(start, end - start).Replace("\\n", "\n");
    }

    static void RequireSource(string path, string text)
    {
        if (!File.ReadAllText(path).Contains(text)) throw new InvalidOperationException("Runtime text changed: " + text);
    }

    static void AddDevelopmentCaption()
    {
        var go = new GameObject("Editorial development provenance", typeof(Canvas));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = .2f;
        canvas.sortingOrder = 1000;
        var label = new GameObject("Provenance", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        var text = label.GetComponent<TextMeshProUGUI>();
        StudyPromptCard.StyleText(text, 22);
        text.text = "DEVELOPMENT · SYNTHETIC STATE · UNITY EDITOR RENDER";
        text.color = new Color(.16f, .16f, .16f);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, 0);
        text.rectTransform.pivot = new Vector2(.5f, 0);
        text.rectTransform.sizeDelta = new Vector2(1500, 50);
        text.rectTransform.anchoredPosition = new Vector2(0, 25);
    }

    static void Capture(string name, Action prepareFrame = null)
    {
        var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        var originalPosition = camera.transform.position;
        var originalRotation = camera.transform.rotation;
        float originalFov = camera.fieldOfView;
        try
        {
            if (name.StartsWith("03-") || name.StartsWith("04-") || name.StartsWith("05-"))
            {
                // A raised editorial view separates the low target from the panel by
                // parallax. Runtime panel/target transforms, fonts and sizes stay intact.
                camera.transform.position += Vector3.up * 1.1f;
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, -1.25f, 1.85f));
            }
            else if (name.StartsWith("01-") || name.StartsWith("02-"))
                camera.transform.rotation = Quaternion.Euler(9, 0, 0);
            else camera.fieldOfView = 40;
            prepareFrame?.Invoke(); // Runtime countdown centering uses the framed camera ray.
            camera.targetTexture = target;
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
            Receipt.AppendLine(name);
            Receipt.AppendLine($"  Camera: {camera.transform.position}, rotation={camera.transform.eulerAngles}, vertical FOV={camera.fieldOfView}");
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                Receipt.AppendLine($"  {text.name}: font={text.font?.name}; overflow={text.isTextOverflowing}; {text.text.Replace('\n', '|')}");
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = previous;
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            camera.fieldOfView = originalFov;
            Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
        }
    }
}
