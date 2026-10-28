using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Explicit timed calibration, independent shape checks, then user acceptance.
/// Target fixation is an instruction/assumption, never inferred from proximity during fitting.</summary>
public sealed class EntryGazeAlignment : MonoBehaviour
{
    public static EntryGazeAlignment Instance { get; private set; }
    public static bool IsShowing => Instance != null && Instance.Active;
    public bool Active { get; private set; }
    public bool Completed { get; private set; }
    public string Outcome { get; private set; } = "not_started";
    SharedGazeStream m_Stream;
    HeadsetPreparationStatus m_Live;
    GameObject m_Panel, m_TargetRoot;
    readonly Transform[] m_Targets = new Transform[3];
    readonly Image[] m_Fills = new Image[3];
    readonly Renderer[] m_Shapes = new Renderer[3];
    readonly Collider[] m_Hitboxes = new Collider[3];
    readonly bool[] m_Seen = new bool[3];
    TextMeshProUGUI m_Text;
    TextMeshProUGUI m_Countdown;
    Transform m_CountdownRoot;
    Image m_CountdownDisc, m_AttentionCue;
    Transform m_AttentionRoot;
    Texture2D m_CueTexture;
    Sprite m_CueSprite;
    int m_PresentedTarget = -1;
    bool m_WasCountingDown;
    double m_AttentionUntil;
    readonly Image[] m_Centers = new Image[3];
    Texture2D m_GlowTexture;
    Sprite m_GlowSprite;
    Button m_Start, m_Skip, m_Ok;
    Texture2D m_Texture;
    Sprite m_Sprite;
    bool m_Started, m_ShowingShapes;
    bool m_StudyVerification;
    string m_EntryId;
    Color Gold => new Color(1, .788f, .016f);
    void Awake() => Instance = this;

    public IEnumerator Run(bool studyVerification = false)
    {
        if (Active || Completed) yield break;
        Instance = this;
        m_StudyVerification=studyVerification;
        GazeDiagnosticView.Instance?.SetVisible(false);
        Active = true; Outcome = "waiting_for_start";
        m_EntryId = Guid.NewGuid().ToString("N");
        m_Live = GetComponent<HeadsetPreparationStatus>();
        m_Stream = SharedGazeStream.Instance;
        m_Stream?.ClearQuickAlignment("Entry calibration awaiting user start.");
        CreatePanel();
        m_Text.text = "<b>Gaze preparation</b>\nSelect Start, then look at the red center of each calibration dot.\nEach dot counts down 3, 2, 1 before recording.\n"+
            (studyVerification?"Five verification dots will follow automatically. Keep the same headset fit.":"Afterward, check three shapes light up and select OK.");
        m_Ok.gameObject.SetActive(!studyVerification);
        m_Live?.Set("preparing", "Waiting to start three-dot calibration");
        while (Active) yield return null;
    }

    void CreatePanel()
    {
        m_Panel = StudyPromptCard.Create(null, "Three-dot calibration", new Vector2(1000, 340));
        m_Panel.transform.localScale = Vector3.one * .001f;
        var canvas = m_Panel.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        m_Panel.AddComponent<TrackedDeviceGraphicRaycaster>();
        var label = new GameObject("Instructions", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(m_Panel.transform, false); m_Text = label.GetComponent<TextMeshProUGUI>();
        m_Text.rectTransform.sizeDelta = new Vector2(940, 210); m_Text.rectTransform.anchoredPosition = new Vector2(0, 40);
        StudyPromptCard.StyleText(m_Text, 28);
        m_Start = RayMenuFeedback.CreateButton(m_Panel.transform, "Start calibration", new Vector2(-265, -120), new Vector2(440, 64), StartCalibration);
        m_Skip = RayMenuFeedback.CreateButton(m_Panel.transform, "Skip (no offset)", new Vector2(105, -120), new Vector2(220, 64), Skip);
        m_Ok = RayMenuFeedback.CreateButton(m_Panel.transform, "OK", new Vector2(355, -120), new Vector2(220, 64), Accept);
        m_Ok.interactable = false;
        AnchorPanel();
    }
    public static Quaternion HorizontalFacing(Quaternion rotation) => Quaternion.Euler(0, rotation.eulerAngles.y, 0);
    void AnchorPanel()
    {
        var head = Camera.main.transform;
        m_Panel.transform.SetPositionAndRotation(head.position + HorizontalFacing(head.rotation) * new Vector3(0, -.28f, 1.7f), HorizontalFacing(head.rotation));
    }
    public void StartCalibration()
    {
        if (!Active || !Application.isFocused || Camera.main == null) return;
        m_Stream = SharedGazeStream.Instance;
        if (m_Stream == null || !m_Stream.isActiveAndEnabled || string.IsNullOrEmpty(m_Stream.RecordingPath) || m_Stream.RecordingError != null)
        { Fail("Eye recording is unavailable. Retry or skip."); return; }
        m_Stream.ClearQuickAlignment("Starting explicit three-dot calibration.");
        ClearTargets(); Array.Clear(m_Seen, 0, 3);
        m_Started = true; m_ShowingShapes = false; Outcome = "countdown";
        m_Ok.interactable = false; m_Start.interactable = false;
        m_Start.GetComponentInChildren<TMP_Text>().text = "Retry calibration";
        AnchorPanel();
        m_Stream.QuickAlignment.BeginGuided(Camera.main.transform.position, HorizontalFacing(Camera.main.transform.rotation), Time.realtimeSinceStartupAsDouble,m_StudyVerification);
        CreateDots(); SetValidationTarget(); UpdateTargetPresentation(0, 3, 0);
    }
    void Update()
    {
        if (!Active || !m_Started) return;
        if (!Application.isFocused) { Fail("Calibration interrupted. Select Retry when ready."); return; }
        if (m_Stream == null || !m_Stream.isActiveAndEnabled || m_Stream.RecordingError != null)
        { Fail("Eye recording is unavailable. Retry or skip."); return; }
        var fit = m_Stream.QuickAlignment;
        if(m_StudyVerification && fit.AwaitingAccuracy){Complete("awaiting_accuracy");return;}
        if (!fit.IsRunning && !fit.AwaitingConfirmation)
        { Fail(fit.Status); return; }
        if (fit.TargetIndex >= 3 && !m_ShowingShapes)
        { ClearTargets(); m_ShowingShapes = true; CreateShapes(); }
        SetValidationTarget();
        int local = fit.TargetIndex % 3;
        double remaining = fit.AwaitingConfirmation ? 0 : fit.CountdownRemaining(Time.realtimeSinceStartupAsDouble);
        UpdateTargetPresentation(local, remaining, fit.PointProgress);
        bool valid = m_Stream.TryGetValid(out var gaze);
        if (m_ShowingShapes)
        {
            for (int i = 0; i < 3; i++)
            {
                bool lit = valid && gaze.headValid && m_Hitboxes[i].Raycast(new Ray(gaze.worldOrigin, fit.PreviewDirection(gaze)), out _, 5);
                m_Shapes[i].sharedMaterial.color = lit ? Gold : new Color(.2f, .5f, .9f);
                if (lit && m_Shapes[i].enabled) m_Seen[i] = true;
            }
        }
        if (fit.AwaitingConfirmation)
        {
            Outcome = "awaiting_confirmation"; m_CountdownRoot.gameObject.SetActive(false);
            foreach (var shape in m_Shapes) if (shape != null) shape.enabled = true;
            m_Ok.interactable = valid && m_Seen[0] && m_Seen[1] && m_Seen[2];
            m_Start.interactable = true;
            m_Text.text = "<b>Check the three shapes</b>\nLook at each shape: it should turn gold while you look at it.\nIf the highlighting matches your gaze, select OK.\nOtherwise, Retry calibration or Skip.";
            m_Live?.Set("preparing", "Confirm all three shapes highlight, then select OK", true, 1);
            return;
        }
        Outcome = remaining > 0 ? "countdown" : "recording";
        m_Text.text = $"<b>{(m_ShowingShapes ? "Check shape" : "Calibration dot")} {local + 1} of 3</b>\n" +
            (m_ShowingShapes ? "Look at the indicated shape. It lights up when your corrected gaze hits it." : "Look at the red center. Keep your head still through the countdown and recording.") +
            "\n" + (valid ? (remaining > 0 ? "Get ready..." : "Recording eye samples...") : "Tracking unavailable: adjust the headset; recording will restart.");
        m_Live?.Set("preparing", (m_ShowingShapes ? "Checking shape " : "Calibrating dot ") + (local + 1), true, (fit.TargetIndex + fit.PointProgress) / 6f);
    }
    void SetValidationTarget()
    {
        var fit = m_Stream.QuickAlignment;
        if (fit.IsRunning) m_Stream.SetValidationTarget(m_Targets[fit.TargetIndex % 3], fit.Report.id, "quick", "target_" + fit.TargetIndex);
        else ReleaseTarget();
    }
    void CreateRoot()
    {
        m_TargetRoot = new GameObject("Entry calibration targets");
        var canvas = new GameObject("Countdown", typeof(RectTransform), typeof(Canvas));
        canvas.transform.SetParent(m_TargetRoot.transform, false);
        canvas.transform.localScale = Vector3.one * .001f;
        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_CountdownRoot = canvas.transform;
        m_CountdownDisc = Disc(canvas.transform, "Countdown background", 130, new Color(.15f, .15f, .15f));
        var text = new GameObject("Number", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(canvas.transform, false);
        m_Countdown = text.GetComponent<TextMeshProUGUI>();
        m_Countdown.fontSize = 88; m_Countdown.alignment = TextAlignmentOptions.Center;
        m_Countdown.color = Color.white; m_Countdown.raycastTarget = false;
        m_Countdown.rectTransform.sizeDelta = new Vector2(120, 120);
        var attention = new GameObject("Target reveal pulse", typeof(RectTransform), typeof(Canvas));
        attention.transform.SetParent(m_TargetRoot.transform, false);
        attention.transform.localScale = Vector3.one * .001f;
        attention.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        m_AttentionRoot = attention.transform;
        m_AttentionCue = Disc(attention.transform, "White attention halo", 240, Color.white);
        m_AttentionCue.sprite = m_CueSprite;
        attention.SetActive(false);
    }
    // Countdown replaces the active fixation center/shape; it never covers it during sampling.
    void UpdateTargetPresentation(int local, double remaining, float progress)
    {
        bool countdown = remaining > 0;
        double now = Time.realtimeSinceStartupAsDouble;
        if (m_PresentedTarget == local && m_WasCountingDown && !countdown)
            m_AttentionUntil = now + GazeQuickAlignment.GuidedAttentionCueSeconds;
        if (m_PresentedTarget != local || countdown) m_AttentionUntil = 0;
        m_PresentedTarget = local; m_WasCountingDown = countdown;
        m_CountdownRoot.gameObject.SetActive(countdown);
        m_CountdownRoot.SetPositionAndRotation(Vector3.MoveTowards(m_Targets[local].position, Camera.main.transform.position, .08f),
            m_Targets[local].rotation);
        m_AttentionRoot.SetPositionAndRotation(m_CountdownRoot.position, m_CountdownRoot.rotation);
        float pulse = Mathf.Clamp01((float)((m_AttentionUntil - now) / GazeQuickAlignment.GuidedAttentionCueSeconds));
        m_AttentionRoot.gameObject.SetActive(pulse > 0);
        m_AttentionCue.color = new Color(1, 1, 1, pulse);
        m_Countdown.text = countdown ? Mathf.CeilToInt((float)remaining).ToString() : "";
        // Dots have their own dark disc. Shapes use the same-sized countdown disc instead.
        m_CountdownDisc.gameObject.SetActive(m_ShowingShapes);
        for (int i = 0; i < 3; i++)
        {
            if (m_Centers[i] != null) m_Centers[i].enabled = !(countdown && i == local);
            if (m_Shapes[i] != null) m_Shapes[i].enabled = !(countdown && i == local);
            if (m_Fills[i] == null) continue;
            float charge = i < local ? 1 : i == local ? progress : 0;
            m_Fills[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(160, 290, charge);
            var color = Gold; color.a = Mathf.Lerp(0, .8f, charge); m_Fills[i].color = color;
        }
    }

    void CreateVisualAssets()
    {
        m_Texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            m_Texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(63 - new Vector2(x - 63.5f, y - 63.5f).magnitude)));
        m_Texture.Apply(); m_Sprite = Sprite.Create(m_Texture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
        m_GlowTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
            m_GlowTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - radius) * 2)));
        }
        m_GlowTexture.Apply(); m_GlowSprite = Sprite.Create(m_GlowTexture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
        m_CueTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude / 64;
            float alpha = Mathf.Clamp01(1 - Mathf.Abs(radius - .8f) / .16f);
            m_CueTexture.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        m_CueTexture.Apply(); m_CueSprite = Sprite.Create(m_CueTexture, new Rect(0, 0, 128, 128), new Vector2(.5f, .5f));
    }
    void CreateDots()
    {
        CreateVisualAssets(); CreateRoot();
        for (int i = 0; i < 3; i++)
        {
            var dot = new GameObject("Calibration dot " + (i + 1), typeof(RectTransform), typeof(Canvas));
            dot.transform.SetParent(m_TargetRoot.transform, false);
            dot.transform.SetPositionAndRotation(m_Stream.QuickAlignment.TargetAt(i), HorizontalFacing(Camera.main.transform.rotation));
            dot.transform.localScale = Vector3.one * .001f; dot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            m_Targets[i] = dot.transform;
            var glow = Disc(dot.transform, "Progress glow", 160, new Color(1, .788f, .016f, 0));
            glow.sprite = m_GlowSprite; m_Fills[i] = glow;
            Disc(dot.transform, "Dot", 160, new Color(.25f, .25f, .25f));
            m_Centers[i] = Disc(dot.transform, "Red fixation center", 18, Color.red);
        }
    }
    Image Disc(Transform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>(); image.sprite = m_Sprite; image.color = color;
        image.rectTransform.sizeDelta = Vector2.one * size; image.raycastTarget = false; return image;
    }
    void CreateShapes()
    {
        CreateVisualAssets(); CreateRoot();
        var types = new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Capsule };
        for (int i = 0; i < 3; i++)
        {
            var go = GameObject.CreatePrimitive(types[i]); go.name = "Check shape " + (i + 1); go.transform.SetParent(m_TargetRoot.transform);
            go.transform.SetPositionAndRotation(m_Stream.QuickAlignment.TargetAt(i + 3), HorizontalFacing(Camera.main.transform.rotation));
            go.transform.localScale = Vector3.one * .13f;
            m_Targets[i] = go.transform; m_Hitboxes[i] = go.GetComponent<Collider>(); m_Shapes[i] = go.GetComponent<Renderer>();
            m_Shapes[i].sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        }
    }
    public void Accept()
    {
        if (!Active || !Application.isFocused || !m_Seen[0] || !m_Seen[1] || !m_Seen[2] || m_Stream == null) return;
        if (m_Stream.ConfirmEntryAlignment()) Complete("passed");
        else Fail("Calibration could not be accepted. Retry or skip.");
    }
    void Fail(string reason)
    {
        m_Stream?.ClearQuickAlignment(reason); ReleaseTarget();
        m_Started = false; Outcome = "needs_attention"; m_Start.interactable = true; m_Ok.interactable = false;
        m_Start.GetComponentInChildren<TMP_Text>().text = "Retry calibration";
        if (m_CountdownRoot != null) m_CountdownRoot.gameObject.SetActive(false);
        if (m_AttentionRoot != null) m_AttentionRoot.gameObject.SetActive(false);
        foreach (var shape in m_Shapes) if (shape != null) shape.enabled = true;
        m_Text.text = "<b>Calibration needs attention</b>\n" + reason + "\nRetry, or Skip (no offset) to continue. No new offset accepted.";
        m_Live?.Set("error", "Calibration: retry or skip");
    }
    public void Skip()
    {
        if (!Active || !Application.isFocused) return;
        m_Stream?.ClearQuickAlignment("Entry calibration skipped."); Complete("skipped");
    }
    void Complete(string outcome)
    {
        Outcome = outcome;
        if (!SaveOutcome()) { Fail("Could not record the outcome. Retry or skip after recording is restored."); return; }
        Completed = true; Active = false;
        ClearTargets(); m_Panel.SetActive(false);
        m_Live?.Set(outcome=="awaiting_accuracy"?"preparing":"ready",
            outcome=="awaiting_accuracy"?"Calibration collected; verifying five dots":"Calibration "+outcome);
    }
    bool SaveOutcome()
    {
        var result = new EntryResult { id = m_EntryId, outcome = Outcome, participant = SharedGazeStream.MeasurementParticipant,
            workflow=m_StudyVerification?"three_fit_then_five_verification":"three_fit_three_shapes_manual_confirmation",
            run = SessionConfig.RunNumber, utc = DateTime.UtcNow.ToString("o"), alignmentId = m_Stream?.QuickAlignment.Report?.id,
            offsetApplied = m_Stream != null && m_Stream.QuickAlignment.IsActive, shapeHighlightSeen = (bool[])m_Seen.Clone(), userConfirmed = Outcome == "passed" };
        try
        {
            var directory = Path.Combine(Application.persistentDataPath, "EyeMeasurements"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "entry_alignment_" + m_EntryId + ".json"), JsonUtility.ToJson(result, true));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        { Debug.LogError("[EntryCalibration] Cannot save acceptance: " + e.Message); m_Stream?.ClearQuickAlignment("Acceptance recording failed."); return false; }
        GetComponent<FindObjectGameManager>()?.RecordReadinessEvent("entry_calibration_" + Outcome);
        return true;
    }
    void ReleaseTarget()
    {
        if (m_Stream != null && m_Stream.ValidationId == m_Stream.QuickAlignment.Report?.id && m_Stream.QuickAlignment.GuidedEntry)
            m_Stream.SetValidationTarget(null, null, null, null);
    }
    void ClearTargets()
    {
        ReleaseTarget();
        for (int i = 0; i < 3; i++) if (m_Shapes[i] != null) DestroyOwned(m_Shapes[i].sharedMaterial);
        if (m_TargetRoot != null) { m_TargetRoot.SetActive(false); DestroyOwned(m_TargetRoot); }
        if (m_Sprite != null) DestroyOwned(m_Sprite); if (m_Texture != null) DestroyOwned(m_Texture);
        if (m_GlowSprite != null) DestroyOwned(m_GlowSprite); if (m_GlowTexture != null) DestroyOwned(m_GlowTexture);
        if (m_CueSprite != null) DestroyOwned(m_CueSprite); if (m_CueTexture != null) DestroyOwned(m_CueTexture);
        m_CueSprite = null; m_CueTexture = null; m_AttentionRoot = null; m_AttentionCue = null;
        m_PresentedTarget = -1; m_WasCountingDown = false; m_AttentionUntil = 0;
        m_TargetRoot = null; m_Sprite = null; m_Texture = null; m_GlowSprite = null; m_GlowTexture = null;
        m_CountdownRoot = null; m_Countdown = null; m_CountdownDisc = null;
        Array.Clear(m_Centers, 0, 3);
        Array.Clear(m_Targets, 0, 3); Array.Clear(m_Fills, 0, 3); Array.Clear(m_Shapes, 0, 3); Array.Clear(m_Hitboxes, 0, 3);
    }
    static void DestroyOwned(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    public void Abort()
    {
        if (Active) { m_Stream?.ClearQuickAlignment("Entry calibration interrupted."); Outcome = "interrupted"; SaveOutcome(); }
        Active = false; ClearTargets(); if (m_Panel != null) m_Panel.SetActive(false);
    }
    public void ResetForRetry()
    {
        Abort(); Completed = false; m_Started = false; Outcome = "not_started";
        if (m_Panel != null) DestroyOwned(m_Panel);
        m_Panel = null;
    }
    void OnDisable() => Abort();
    void OnDestroy() { if (m_Panel != null) DestroyOwned(m_Panel); if (Instance == this) Instance = null; }
    [Serializable] sealed class EntryResult
    {
        public int schema = 4, run; public string id, outcome, participant, utc, alignmentId, workflow;
        public bool assumedFixation = true, userConfirmed, offsetApplied; public bool[] shapeHighlightSeen;
    }
}
