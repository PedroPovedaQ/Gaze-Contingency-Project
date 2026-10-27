using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Unscored look-and-trigger rehearsal using the study's gaze winner and controller selector.</summary>
[DefaultExecutionOrder(250)]
public sealed class ActivationExercise : MonoBehaviour
{
    public bool Active { get; private set; }
    public bool Completed { get; private set; }
    public bool RecalibrationRequested { get; private set; }
    public int Step => m_Progress.Step;
    public bool AcceptingInput => Active && !m_Progress.Finished && Application.isFocused &&
        (m_Controls == null || !m_Controls.SettingsOpen) && m_SaveError == null;
    public IReadOnlyList<GameObject> Objects => m_Objects;
    readonly List<GameObject> m_Objects = new();
    readonly List<Attempt> m_Attempts = new();
    readonly string[] m_Names = { "red cube", "blue sphere", "yellow capsule" };
    ActivationCheckProgress m_Progress = new();
    ControllerRaySelector m_Selector;
    bool m_PreviousThroughWallSearch;
    StudyRayControls m_Controls;
    GameObject m_Panel;
    TextMeshProUGUI m_Text;
    Button m_Continue;
    string m_Id, m_SaveError, m_Feedback;
    double m_FeedbackUntil;

    public IEnumerator Run()
    {
        if (Active) yield break;
        ClearVisuals(); m_Progress = new ActivationCheckProgress(); m_Attempts.Clear();
        m_Id = Guid.NewGuid().ToString("N"); m_SaveError = null; m_Feedback = null;
        Completed = RecalibrationRequested = false; Active = true;
        m_Controls = GetComponent<StudyRayControls>();
        m_Selector = GetComponent<ControllerRaySelector>() ?? gameObject.AddComponent<ControllerRaySelector>();
        m_PreviousThroughWallSearch = m_Selector.ThroughWallSearch;
        m_Selector.ThroughWallSearch = true; // Match the rotational study's object-only physics ray.
        m_Selector.ResetSelection(); m_Selector.Exercise = this;
        CreateVisuals(); Save("in_progress");
        GetComponent<HeadsetPreparationStatus>()?.Set("preparing", "Look-and-select activation exercise");
        while (Active) yield return null;
    }

    void CreateVisuals()
    {
        var head = Camera.main.transform;
        var rotation = EntryGazeAlignment.HorizontalFacing(head.rotation);
        var origin = head.position;
        var types = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule };
        var colors = new[] { new Color(.9f,.15f,.15f), new Color(.15f,.35f,.9f), new Color(.95f,.85f,.1f) };
        for (int i = 0; i < 3; i++)
        {
            var obj = GameObject.CreatePrimitive(types[i]); obj.SetActive(false);
            obj.name = "Activation " + m_Names[i]; obj.layer = 8;
            obj.transform.SetPositionAndRotation(origin + rotation * new Vector3((i - 1) * .55f, .05f, 1.5f), rotation);
            obj.transform.localScale = Vector3.one * (i == 2 ? .0455f : .08f);
            var info = obj.AddComponent<SpawnableObjectInfo>();
            info.objectId = "activation_" + i; info.shapeName = types[i].ToString(); info.colorName = new[] { "Red", "Blue", "Yellow" }[i];
            var rb = obj.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            obj.AddComponent<XRGrabInteractable>();
            var material = new Material(Shader.Find("Shader Graphs/InteractablePrimitive") ?? Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colors[i]);
            material.SetColor("_Color", colors[i]);
            material.SetColor("_EdgeHighlightColor", Color.clear);
            material.SetFloat("_EdgeHighlightFalloff", 1.5f);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            ShapeObjectFactory.PrepareInteractable(obj); obj.SetActive(true); ShapeObjectFactory.ActivateInteractable(obj);
            m_Objects.Add(obj);
        }
        Physics.SyncTransforms();
        m_Panel = StudyPromptCard.Create(null, "Activation exercise", new Vector2(1100, 340));
        m_Panel.transform.SetPositionAndRotation(origin + rotation * new Vector3(0, -.4f, 1.5f), rotation);
        m_Panel.transform.localScale = Vector3.one * .001f;
        var canvas = m_Panel.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        m_Panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        var label = new GameObject("Instructions", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(m_Panel.transform, false); m_Text = label.GetComponent<TextMeshProUGUI>();
        m_Text.rectTransform.sizeDelta = new Vector2(1040, 220); m_Text.rectTransform.anchoredPosition = new Vector2(0, 40);
        StudyPromptCard.StyleText(m_Text, 27);
        RayMenuFeedback.CreateButton(m_Panel.transform, "Recalibrate", new Vector2(-260,-120), new Vector2(440,64), Recalibrate);
        m_Continue = RayMenuFeedback.CreateButton(m_Panel.transform, "Continue", new Vector2(260,-120), new Vector2(440,64), Continue);
        m_Continue.interactable = false;
    }

    int ObserveGaze()
    {
        var stream = SharedGazeStream.Instance;
        int index = -1;
        GazeObservation gaze = null;
        bool usable = AcceptingInput && stream != null && stream.TryGetValid(out gaze);
        if (usable)
        {
            var vicinity = GetComponent<GazeTargetVicinity>();
            var winner = GazeTargetVicinity.ClosestObject(gaze.worldOrigin, gaze.worldDirection, m_Objects,
                vicinity != null ? vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees, out _, out _, out _);
            index = m_Objects.IndexOf(winner);
        }
        m_Progress.Observe(usable, index, Time.realtimeSinceStartupAsDouble);
        return index;
    }
    void LateUpdate()
    {
        if (!Active) return;
        int gazeObject = ObserveGaze();
        bool show = Application.isFocused && (m_Controls == null || !m_Controls.SettingsOpen);
        foreach (var obj in m_Objects) if (obj != null) obj.SetActive(show && !m_Progress.Finished);
        m_Panel.SetActive(show);
        m_Continue.interactable = m_Progress.Finished;
        string feedback = Time.realtimeSinceStartupAsDouble < m_FeedbackUntil ? m_Feedback : "";
        m_Text.text = m_SaveError != null ? "<b>Could not save activation check</b>\nAsk the researcher to restore recording storage, then select Recalibrate." :
            m_Progress.Finished ? "<b>Activation check complete · 3/3</b>\nYou looked at and selected each intended object.\nSelect Continue to center the experiment. This exercise is unscored." :
            $"<b>Look and select · {m_Progress.Step + 1}/3</b>\nLook at the <b>{m_Names[m_Progress.Step]}</b> until it brightens.\n" +
            (m_Progress.Ready ? "Keep looking. Point with the RIGHT controller and pull the trigger." : "Hold your gaze briefly, then point with the RIGHT controller and pull the trigger.") +
            (string.IsNullOrEmpty(feedback) ? "\nGaze response: " + (gazeObject >= 0 ? m_Names[gazeObject] : "no object") +
                ". Recalibrate if this does not match." : "\n" + feedback);
    }
    public bool Contains(GameObject target) => target != null && m_Objects.Contains(target);
    public void Capture(GameObject target)
    {
        if (!AcceptingInput || !Contains(target)) return;
        int gaze = ObserveGaze(), expected = m_Progress.Step, selected = m_Objects.IndexOf(target);
        string result = m_Progress.Select(selected);
        m_Attempts.Add(new Attempt { time = Time.realtimeSinceStartupAsDouble, expected = expected, selected = selected,
            gaze = gaze, result = result });
        m_Feedback = result == "correct" ? "Correct — next object." : result == "wrong_object" ?
            "You selected the " + m_Names[selected] + ". Try the named object." : "Look at the named object until it brightens, then try again.";
        m_FeedbackUntil = Time.realtimeSinceStartupAsDouble + 2;
        m_Selector.ResetSelection(); Save("in_progress");
    }
    public void Continue()
    {
        if (!Application.isFocused) return;
        Complete("passed");
    }
    public void Recalibrate()
    {
        if (!Application.isFocused) return;
        Complete("recalibrate");
    }
    void Complete(string outcome)
    {
        if (!Active || (outcome == "passed" && !m_Progress.Finished) || !Save(outcome)) return;
        Completed = outcome == "passed";
        RecalibrationRequested = outcome == "recalibrate";
        Finish();
    }
    bool Save(string outcome)
    {
        try
        {
            var directory = Path.Combine(Application.persistentDataPath, "EyeMeasurements"); Directory.CreateDirectory(directory);
            var report = new Report { id = m_Id, participant = SessionConfig.ParticipantId, run = SessionConfig.RunNumber,
                utc = DateTime.UtcNow.ToString("o"), outcome = outcome, completedObjects = m_Progress.Step,
                alignmentId = SharedGazeStream.Instance?.QuickAlignment.Report?.id,
                eyeRecordingId = SharedGazeStream.Instance?.Latest?.recordingId,
                offsetApplied = SharedGazeStream.Instance?.QuickAlignment.IsActive == true, attempts = m_Attempts.ToArray() };
            File.WriteAllText(Path.Combine(directory, "activation_" + m_Id + ".json"), JsonUtility.ToJson(report, true));
            m_SaveError = null; return true;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { m_SaveError = e.Message; Debug.LogError("[ActivationExercise] " + e.Message); return false; }
    }
    void Finish()
    {
        Active = false; m_Selector?.ResetSelection();
        if (m_Selector != null && m_Selector.Exercise == this)
        {
            m_Selector.Exercise = null;
            m_Selector.ThroughWallSearch = m_PreviousThroughWallSearch;
        }
        ClearVisuals();
        GetComponent<HeadsetPreparationStatus>()?.Set("preparing", Completed ? "Activation check passed; ready to center" : "Retrying calibration");
    }
    public void Abort() { if (Active) { Save("interrupted"); Finish(); } }
    void OnDisable() => Abort();
    void OnDestroy() => ClearVisuals();
    void ClearVisuals()
    {
        foreach (var obj in m_Objects) if (obj != null)
        { obj.SetActive(false); Dispose(obj.GetComponent<Renderer>().sharedMaterial); Dispose(obj); }
        m_Objects.Clear();
        if (m_Panel != null) { m_Panel.SetActive(false); Dispose(m_Panel); m_Panel = null; }
    }
    static void Dispose(UnityEngine.Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    [Serializable] sealed class Attempt { public double time; public int expected, selected, gaze; public string result; }
    [Serializable] sealed class Report
    {
        public int schema = 1, run, completedObjects; public string id, participant, utc, outcome, alignmentId, eyeRecordingId;
        public bool offsetApplied, scored = false; public double gazeHoldSeconds = ActivationCheckProgress.HoldSeconds;
        public string purpose = "familiarization_not_calibration_validation"; public Attempt[] attempts;
    }
}
