using System;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public static class RayMenuChecks
{
    static object Read(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Button ButtonIn(GameObject panel, string name) => Array.Find(panel.GetComponentsInChildren<Button>(), b => b.name == name);
    static void Click(Button button, EventSystem events)
    {
        Check(button != null && button.IsInteractable(), "Expected an enabled pointed-at button");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(events) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    [MenuItem("Tools/Codex/Check Ray Menus")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before checks.");
        var gender = SessionConfig.NeutralProfile; int option = SessionConfig.NeutralVoiceOption;
        var voice = SessionConfig.Voice;
        var host = new GameObject("RayMenuFixture"); host.SetActive(false);
        var eventHost = new GameObject("RayMenuEvents", typeof(EventSystem));
        GameObject panel = null, surveyCanvas = null;
        var events = eventHost.GetComponent<EventSystem>();
        try
        {
            var selector = host.AddComponent<VoiceModeSelector>();
            selector.Initialize(null);
            panel = (GameObject)Read(selector, "m_CanvasGO");
            var card = panel.GetComponentInChildren<StudyPromptCard>();
            Check(card != null && card.GetComponent<Image>().sprite != null, "Setup has the shared rounded coaching card");
            Check(card.GetComponent<Image>().raycastTarget, "Card backgrounds retain controller ray blocking");
            Call(selector, "Update");
            Capture(panel, "/tmp/gaze-prompt-start.png");
            Check(panel.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "Setup uses XRI tracked raycaster");
            typeof(VoiceModeSelector).GetField("m_PerspectiveChosen", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(selector, true);
            var phase = typeof(VoiceModeSelector).GetField("m_Phase", BindingFlags.NonPublic | BindingFlags.Instance);
            phase.SetValue(selector, Enum.Parse(phase.FieldType, "Choosing"));
            selector.SelectGeneric(); Call(selector, "RefreshChoices");
            var male = ButtonIn(panel, "Male");
            ExecuteEvents.Execute(male.gameObject, new PointerEventData(events), ExecuteEvents.pointerEnterHandler);
            Check(Read(selector, "m_Phase").ToString() == "ChoosingNeutral", "Hover alone never chooses gender");
            ExecuteEvents.Execute(male.gameObject, new PointerEventData(events), ExecuteEvents.pointerDownHandler);
            Check(Read(selector, "m_Phase").ToString() == "ChoosingNeutral", "Pointer down alone cannot skip a screen");
            Click(male, events); Call(selector, "RefreshChoices");
            Check(ButtonIn(panel, "Eric") != null && ButtonIn(panel, "Roger") != null, "Male cards are available");
            Check(panel.GetComponentsInChildren<VoicePreviewHover>().Length == 6, "All six male voices are auditionable");
            Check(panel.GetComponentsInChildren<RawImage>().Length == 6, "Every male card has its own portrait");
            foreach (var image in panel.GetComponentsInChildren<RawImage>()) Check(image.texture != null, "Each card loads its avatar");
            var eric = ButtonIn(panel, "Eric");
            var pointer = new PointerEventData(events) { pointerId = 101 };
            ExecuteEvents.Execute(eric.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Check((int)Read(selector, "m_PreviewOption") == 0 && (int)Read(selector, "m_SelectedVoiceOption") == -1,
                "Pointing at Eric starts an audition without selecting");
            int request = (int)Read(selector, "m_PreviewRequest");
            ExecuteEvents.Execute(eric.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(eric.gameObject, new PointerEventData(events) { pointerId = 102 }, ExecuteEvents.pointerEnterHandler);
            Check((int)Read(selector, "m_PreviewRequest") == request, "Steady hover or second ray does not restart sample");
            ExecuteEvents.Execute(eric.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(eric.gameObject, new PointerEventData(events) { pointerId = 102 }, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(eric.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Check((int)Read(selector, "m_PreviewRequest") == request + 1, "Leave and reenter permits replay/retry");
            Check(ButtonIn(panel, "Back") != null, "Hover retains Back until a trigger selection");
            Click(ButtonIn(panel, "Roger"), events); Call(selector, "RefreshChoices");
            Check(ButtonIn(panel, "Eric") != null && ButtonIn(panel, "Roger") != null, "Selecting a voice keeps both avatars on screen");
            Check(ButtonIn(panel, "Confirm") != null && ButtonIn(panel, "Back") == null, "Selection changes Back to Confirm");
            Check(ButtonIn(panel, "Roger").GetComponent<Outline>().enabled && !eric.GetComponent<Outline>().enabled,
                "Only chosen card has persistent selection border");
            Check(!ButtonIn(panel, "Confirm").interactable, "Unavailable audio disables Confirm");
            Capture(panel, "/tmp/gaze-ray-menu-male-six.png");
            selector.PreviewNeutralVoiceOption(0);
            Check((int)Read(selector, "m_SelectedVoiceOption") == 1 && ButtonIn(panel, "Roger").GetComponent<Outline>().enabled,
                "Previewing Eric preserves selected Roger border");
            Check(SessionConfig.NeutralProfile == gender && SessionConfig.NeutralVoiceOption == option,
                "Auditions and pending selections never mutate committed voice");
            selector.ConfirmNeutralVoice();
            Check(Read(selector, "m_Phase").ToString() == "ChoosingNeutralVoice", "Cannot accept absent audio");
            Click(eric, events);
            Check(eric.GetComponent<Outline>().enabled && !ButtonIn(panel, "Roger").GetComponent<Outline>().enabled,
                "Trigger can change selected border");
            selector.SelectGeneric(); Call(selector, "RefreshChoices");
            Click(ButtonIn(panel, "Female"), events); Call(selector, "RefreshChoices");
            Check(ButtonIn(panel, "Janet") != null && ButtonIn(panel, "Sarah") != null, "Female cards are available");
            Check(panel.GetComponentsInChildren<VoicePreviewHover>().Length == 6, "All six female voices are auditionable");
            Check(panel.GetComponentsInChildren<RawImage>().Length == 6, "Every female card has its own portrait");
            foreach (var image in panel.GetComponentsInChildren<RawImage>()) Check(image.texture != null, "Each female card loads its avatar");
            Check(ButtonIn(panel, "Back") != null && (int)Read(selector, "m_SelectedVoiceOption") == -1,
                "Changing gender resets selection and restores Back");
            Capture(panel, "/tmp/gaze-ray-menu-female-six.png");
            var ui = host.AddComponent<FindObjectUI>(); ui.Initialize();
            Capture((GameObject)Read(ui,"m_CanvasGO"), "/tmp/gaze-prompt-center.png");
            ui.ShowObjective(Color.red, "Cube", 0, 10);
            var status = (TMPro.TextMeshProUGUI)Read(ui, "m_ObjectiveText");
            Check(status.text == "", "Timing panel does not reveal the target");
            Check(((TMPro.TextMeshProUGUI)Read(ui, "m_TimerText")).enabled &&
                ((TMPro.TextMeshProUGUI)Read(ui, "m_ProgressText")).enabled,
                "Timer and trial progress remain visible");
            ui.ShowRoundAudioWait(true);
            Check(status.text.Contains("Listen to the agent") && !status.text.Contains("Cube"),
                "Readiness status remains available without target identity");
            ui.ShowWrongFeedback();
            Check(status.text.Contains("Wrong object"), "Wrong-selection feedback remains available");
            ui.ShowRoundAudioWait(false);
            Check(status.text == "", "Clearing readiness does not restore target text");
            ui.ShowBlockSurvey(1);
            var canvas = (GameObject)Read(ui, "m_CanvasGO");
            surveyCanvas = canvas; canvas.transform.SetParent(null);
            Check(canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null, "Survey uses XRI tracked raycaster");
            var sliders = (Slider[])Read(ui, "m_NasaTlxSliders");
            for (int i = 0; i < sliders.Length; i++) sliders[i].value = 10 + i * 10;
            FindObjectUI.NasaTlxResult result = default; int submissions = 0;
            ui.OnNasaTlxSubmitted += value => { result = value; submissions++; };
            Click(ButtonIn(canvas, "NasaSubmitButton"), events);
            Click(ButtonIn(canvas, "NasaSubmitButton"), events);
            Check(submissions == 1 && result.mental == 10 && result.physical == 20 && result.temporal == 30 &&
                result.performance == 40 && result.effort == 50 && result.frustration == 60, "Ray submission saves all six slider values once");
            int finished = 0; ui.OnStatsDismissed += () => finished++;
            ui.ShowPostSurveyStats("Test stats"); Click(ButtonIn(canvas, "Finish"), events);
            Check(finished == 1, "Finish action is reachable through a visible button");
            ui.ShowBlockSurvey(1);
            Capture((GameObject)Read(ui,"m_CanvasGO"), "/tmp/gaze-prompt-survey.png");
            ui.ShowThankYouMessage();
            Capture((GameObject)Read(ui,"m_CanvasGO"), "/tmp/gaze-prompt-thank-you.png");
            Debug.Log("[RayMenuChecks] PASS: hover audition without selection; no steady-hover/second-ray restarts; reentry retry; persistent exclusive border; Back becomes Confirm; no-audio gate; twelve voice cards and portraits; XRI raycasters; survey and finish. Previews: /tmp/gaze-ray-menu-{male,female}-six.png");
        }
        finally
        {
            if (panel != null) UnityEngine.Object.DestroyImmediate(panel);
            if (surveyCanvas != null) UnityEngine.Object.DestroyImmediate(surveyCanvas);
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(eventHost);
            SessionConfig.SelectNeutralVoice(gender, option); SessionConfig.Voice = voice;
        }
    }

    static void Capture(GameObject panel, string output)
    {
        var cameraGO = new GameObject("MenuPreviewCamera", typeof(Camera));
        var camera = cameraGO.GetComponent<Camera>();
        var oldParent = panel.transform.parent; var oldPos = panel.transform.localPosition; var oldRot = panel.transform.localRotation;
        var target = new RenderTexture(1000, 800, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1000, 800, TextureFormat.RGB24, false);
        var layers = new System.Collections.Generic.Dictionary<GameObject, int>();
        try
        {
            foreach (var child in panel.GetComponentsInChildren<Transform>(true)) { layers[child.gameObject] = child.gameObject.layer; child.gameObject.layer = 31; }
            panel.transform.SetParent(null); panel.transform.position = new Vector3(0, 0, 1.2f); panel.transform.rotation = Quaternion.identity;
            camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.1f, 0.13f);
            camera.fieldOfView = 35; camera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1000, 800), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(output, image.EncodeToPNG());
        }
        finally
        {
            foreach (var item in layers) if (item.Key != null) item.Key.layer = item.Value;
            panel.transform.SetParent(oldParent, false); panel.transform.localPosition = oldPos; panel.transform.localRotation = oldRot;
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraGO);
        }
    }
}
