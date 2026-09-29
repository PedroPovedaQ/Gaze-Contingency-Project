using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// On-entry setup: choose neutral gender, then one of two voices, hear/confirm
/// the sample, then enroll the self-similar voice. Shown once at startup
/// as a small world-space panel in front of the camera.
///
/// Point at a visible control, then pull the trigger to select it.
///
/// The researcher may also just call SelectNeutralMale()/SelectNeutralFemale()/SelectSelfSimilar() or set
/// the default gender in the inspector; a named voice still needs confirmation.
/// </summary>
public class VoiceModeSelector : MonoBehaviour
{
    const string k_Tag = "[VoiceMode]";

    // Short task commands read aloud during enrollment at a natural guiding pace.
    const string k_ReadingPassage =
        "Locate the red sphere. Look toward the upper shelf. " +
        "Move your gaze slowly to the right. Check the blue cube near the center. " +
        "Compare each object's color and shape. Scan the lower row from left to right. " +
        "Look beside the purple cylinder. Point the controller ray at the target and check its highlight. " +
        "Press the trigger to select the matching object. Take a short pause. Get ready for the next round.";

    [Header("Optional overrides")]
    [SerializeField] bool m_AutoConfirmDefault = false; // preselect gender; still ask for a voice
    [SerializeField] int m_RecordSeconds = 40;

    [SerializeField] NeutralVoiceProfile m_DefaultNeutralProfile = NeutralVoiceProfile.Female;

    public event System.Action SelectionCompleted;
    public bool IsComplete => m_Phase == Phase.Done;

    enum Phase { WaitingToStart, Choosing, ChoosingNeutral, ChoosingNeutralVoice, Enrolling, EnrollmentFailed, Preparing, PreparationFailed, CheckingNeutral, CheckingSelf, Cancelled, Done }
    NeutralVoiceProfile m_ChosenGender;
    Phase m_Phase = Phase.WaitingToStart;

    VoiceEnrollment m_Enrollment;
    VoiceSynthesizer m_Synth;
    Canvas m_Canvas;
    GameObject m_CanvasGO;
    TextMeshProUGUI m_Text;
    Button m_FinishRecordingButton;
    TextMeshProUGUI m_ActionLabel;
    bool m_StartArmed;
    float m_StartReleasedAt = -1f;
    float m_SetupInputAfter;
    readonly List<InputDevice> m_Devices = new List<InputDevice>();
    bool m_PerspectiveChosen;
    readonly List<Button> m_Choices = new();
    Phase m_RenderedPhase = (Phase)(-1);
    int m_SelectedVoiceOption = -1;
    int m_PreviewOption = -1;
    int m_PreviewRequest;
    readonly bool[] m_HeardVoice = new bool[2];
    readonly Outline[] m_VoiceBorders = new Outline[2];
    Button m_VoiceActionButton;
    Button m_AcceptButton;


    public void Initialize(VoiceEnrollment enrollment, VoiceSynthesizer synth = null)
    {
        m_Enrollment = enrollment;
        m_Synth = synth;
        m_PerspectiveChosen = false;
        BuildPanel();
        m_Phase = Phase.WaitingToStart;
        SetText("<b>Ready to begin?</b>\n\nDismiss any headset notices first.\nRelease the controller buttons, then select\n<b>Start setup</b> below.");
    }

    public void StartSetup()
    {
        if (m_Phase != Phase.WaitingToStart || !m_StartArmed || !Application.isFocused) return;
        m_StartArmed = false;
        m_Phase = Phase.Choosing;
        m_SetupInputAfter = Time.realtimeSinceStartup + 0.75f;
        m_FinishRecordingButton.gameObject.SetActive(false);
        // Wording is fixed by voice condition: external vs first-person singular.
        if (!SessionConfig.TrySetPerspective(VoicePerspective.External))
        {
            SetText("<b>Wording already locked</b>\nReset the session before starting a new setup.");
            return;
        }
        m_PerspectiveChosen = true;
        BeginVoiceSelection();
    }

    void BuildPanel()
    {
        m_CanvasGO = new GameObject("VoiceModeCanvas");
        var cam = Camera.main;
        if (cam != null) m_CanvasGO.transform.SetParent(cam.transform, false);
        m_CanvasGO.transform.localPosition = new Vector3(0f, 0f, 1.2f);
        m_CanvasGO.transform.localRotation = Quaternion.identity;

        m_Canvas = m_CanvasGO.AddComponent<Canvas>();
        m_Canvas.renderMode = RenderMode.WorldSpace;
        m_CanvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();
        var rect = m_CanvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(700, 480);
        m_CanvasGO.transform.localScale = Vector3.one * 0.0011f;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(m_CanvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(700, 480);
        bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.82f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(m_CanvasGO.transform, false);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(660, 350);
        tRect.anchoredPosition = new Vector2(0, 40);
        m_Text = textGO.AddComponent<TextMeshProUGUI>();
        m_Text.alignment = TextAlignmentOptions.Center;
        m_Text.fontSize = 34;
        m_Text.enableAutoSizing = true;
        m_Text.fontSizeMin = 22;
        m_Text.fontSizeMax = 34;
        m_Text.raycastTarget = false;
        m_Text.color = new Color(0.9f, 0.97f, 1f, 1f);

        var buttonGO = new GameObject("FinishRecording");
        buttonGO.transform.SetParent(m_CanvasGO.transform, false);
        var buttonRect = buttonGO.AddComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(440, 64);
        buttonRect.anchoredPosition = new Vector2(0, -190);
        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.12f, 0.4f, 0.65f, 1f);
        m_FinishRecordingButton = buttonGO.AddComponent<Button>();
        m_FinishRecordingButton.targetGraphic = image;
        RayMenuFeedback.Attach(m_FinishRecordingButton);
        m_FinishRecordingButton.onClick.AddListener(() =>
        {
            if (m_Phase == Phase.WaitingToStart) StartSetup();
            else FinishRecording();
        });
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(buttonGO.transform, false);
        var labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.sizeDelta = buttonRect.sizeDelta;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        m_ActionLabel = label;
        label.text = "Finish recording";
        label.fontSize = 25;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        buttonGO.SetActive(false);
    }

    public void FinishRecording() => m_Enrollment?.FinishRecording();

    void OnApplicationFocus(bool focused)
    {
        if (focused) return;
        m_StartArmed = false;
        m_StartReleasedAt = -1f;
        if (m_Phase == Phase.ChoosingNeutralVoice)
        {
            m_PreviewRequest++;
            m_Synth?.Stop();
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) OnApplicationFocus(false);
    }

    void SetText(string s) { if (m_Text != null) m_Text.text = s; }

    void Update()
    {
        // Keep the panel in front of the camera if we couldn't parent at build time.
        if (m_CanvasGO != null && m_CanvasGO.transform.parent == null)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                m_CanvasGO.transform.position = cam.transform.position + cam.transform.forward * 1.2f;
                m_CanvasGO.transform.rotation = Quaternion.LookRotation(m_CanvasGO.transform.position - cam.transform.position);
            }
        }

        if (m_Phase == Phase.WaitingToStart)
        {
            bool released = !ReadTrigger() && !ReadPrimary() && !ReadButton(CommonUsages.secondaryButton);
            if (!Application.isFocused)
            {
                m_StartArmed = false;
                m_StartReleasedAt = -1f;
            }
            else if (!m_StartArmed)
            {
                if (!released) m_StartReleasedAt = -1f;
                else if (m_StartReleasedAt < 0) m_StartReleasedAt = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - m_StartReleasedAt >= 0.75f) m_StartArmed = true;
            }
            m_FinishRecordingButton.gameObject.SetActive(true);
            m_FinishRecordingButton.interactable = m_StartArmed;
            m_ActionLabel.text = "Start setup";
            // Only the pointed-at button starts setup; a global trigger press may belong to the OS notice.
            return;
        }

        if (m_FinishRecordingButton != null)
        {
            bool recording = m_Phase == Phase.Enrolling && m_Enrollment != null &&
                m_Enrollment.Current == VoiceEnrollment.State.Recording;
            m_FinishRecordingButton.gameObject.SetActive(recording);
            m_FinishRecordingButton.interactable = recording && m_Enrollment.CanFinishRecording;
            m_ActionLabel.text = "Finish recording";
        }
        RefreshChoices();

        bool available = Application.isFocused && Time.realtimeSinceStartup >= m_SetupInputAfter;
        foreach (var button in m_Choices) button.interactable = available;
        if (m_AcceptButton != null)
            m_AcceptButton.interactable = available && m_Synth != null && !m_Synth.IsBusy && string.IsNullOrEmpty(m_Synth.LastError);
        if (m_VoiceActionButton != null && m_SelectedVoiceOption >= 0)
            m_VoiceActionButton.interactable = available && CanConfirmNeutralVoice();
    }

    // Keep cards stable during previews and selection so pointer entry does not restart audio.
    void RefreshChoices()
    {
        if (m_CanvasGO == null || m_RenderedPhase == m_Phase) return;
        foreach (var button in m_Choices)
        {
            button.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(button.gameObject); else DestroyImmediate(button.gameObject);
        }
        m_Choices.Clear(); m_AcceptButton = null; m_VoiceActionButton = null;
        m_RenderedPhase = m_Phase;
        bool cards = m_Phase == Phase.ChoosingNeutralVoice;
        m_Text.rectTransform.sizeDelta = new Vector2(660, cards ? 120 : m_Phase == Phase.Enrolling ? 350 : 220);
        m_Text.rectTransform.anchoredPosition = new Vector2(0, cards ? 160 : m_Phase == Phase.Enrolling ? 40 : 100);
        switch (m_Phase)
        {
            case Phase.ChoosingNeutral:
                Choice("Male", SelectNeutralMale, -170, -100, 300);
                Choice("Female", SelectNeutralFemale, 170, -100, 300);
                break;
            case Phase.ChoosingNeutralVoice:
                for (int i = 0; i < 2; i++)
                {
                    int option = i;
                    string name = SessionConfig.NeutralName(m_ChosenGender, i);
                    var button = Choice(name, () => SelectNeutralVoiceOption(option), i == 0 ? -165 : 165, -15, 280, 210);
                    button.gameObject.AddComponent<VoicePreviewHover>().Initialize(() => PreviewNeutralVoiceOption(option));
                    var border = button.gameObject.AddComponent<Outline>();
                    border.effectColor = new Color(1f, 0.8f, 0.2f);
                    border.effectDistance = new Vector2(4, -4);
                    border.useGraphicAlpha = false;
                    border.enabled = false;
                    m_VoiceBorders[i] = border;
                    var avatar = new GameObject("Avatar", typeof(RectTransform), typeof(RawImage));
                    avatar.transform.SetParent(button.transform, false);
                    avatar.GetComponent<RectTransform>().sizeDelta = new Vector2(140, 140);
                    avatar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 24);
                    avatar.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("VoiceAvatars/" + name);
                    avatar.GetComponent<RawImage>().raycastTarget = false;
                    button.GetComponentInChildren<TextMeshProUGUI>().rectTransform.anchoredPosition = new Vector2(0, -75);
                }
                m_VoiceActionButton = Choice("Back", () =>
                {
                    if (m_SelectedVoiceOption < 0) SelectGeneric(); else ConfirmNeutralVoice();
                }, 0, -190, 240);
                RefreshVoiceSelection();
                break;
            case Phase.EnrollmentFailed:
                Choice("Record again", SelectSelfSimilar, -170, -175, 300);
                Choice("Cancel session", CancelSetup, 170, -175, 300);
                break;
            case Phase.PreparationFailed:
                if (m_Synth != null && !m_Synth.ProviderPolicyBlocked)
                {
                    Choice("Retry audio", RetryPreparation, -170, -90, 300);
                    Choice("Record again", SelectSelfSimilar, 170, -90, 300);
                }
                Choice("Cancel session", CancelSetup, 0, -185, 300);
                break;
            case Phase.CheckingNeutral:
            case Phase.CheckingSelf:
                m_AcceptButton = Choice("Accept audio", ConfirmAudioSample, 0, -80, 440);
                Choice("Replay", ReplayAudioSample, -170, -175, 300);
                Choice("Restart setup", SelectGeneric, 170, -175, 300);
                break;
        }
    }

    Button Choice(string label, UnityEngine.Events.UnityAction action, float x, float y, float width, float height = 64)
    {
        var button = RayMenuFeedback.CreateButton(m_CanvasGO.transform, label, new Vector2(x, y), new Vector2(width, height), action);
        m_Choices.Add(button);
        return button;
    }

    public void RetryPreparation()
    {
        if (m_Phase == Phase.PreparationFailed && m_Synth != null && !m_Synth.ProviderPolicyBlocked)
            StartCoroutine(PrepareVoices());
    }

    public void ReplayAudioSample()
    {
        if (m_Phase == Phase.CheckingNeutral || m_Phase == Phase.CheckingSelf)
            m_Synth?.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
    }

    void BeginVoiceSelection()
    {
        m_Phase = Phase.Choosing;
        if (m_AutoConfirmDefault)
        {
            SelectNeutral(m_DefaultNeutralProfile);
            return;
        }
        SelectGeneric();
    }

    public void SelectGeneric()
    {
        if (!m_PerspectiveChosen || m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        m_PreviewRequest++;
        m_SelectedVoiceOption = m_PreviewOption = -1;
        m_Synth?.ResetVoicePreparation();
        m_Phase = Phase.ChoosingNeutral;
        SetText("<b>Choose your agent’s voice</b>\n\nPoint at your preferred gender, then pull the trigger.");
    }

    public void SelectNeutralMale() => SelectNeutral(NeutralVoiceProfile.Male);
    public void SelectNeutralFemale() => SelectNeutral(NeutralVoiceProfile.Female);

    void SelectNeutral(NeutralVoiceProfile profile)
    {
        if (!m_PerspectiveChosen || m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        m_PreviewRequest++;
        m_Synth?.Stop();
        m_ChosenGender = profile;
        m_RenderedPhase = (Phase)(-1);
        m_SelectedVoiceOption = m_PreviewOption = -1;
        System.Array.Clear(m_HeardVoice, 0, m_HeardVoice.Length);
        m_Phase = Phase.ChoosingNeutralVoice;
        SetText("<b>Point your controller at a voice to hear it.</b>\nPull the trigger to select, then choose Confirm.");
    }

    /// <summary>Audition without changing the pending or committed voice choice.</summary>
    public void PreviewNeutralVoiceOption(int option)
    {
        if (m_Phase != Phase.ChoosingNeutralVoice || option < 0 || option > 1) return;
        if (m_PreviewOption == option && m_Synth != null && m_Synth.IsBusy) return;
        m_PreviewOption = option;
        int request = ++m_PreviewRequest;
        string name = SessionConfig.NeutralName(m_ChosenGender, option);
        SetText($"<b>Listen: {name}</b>\nPull the trigger to select, then choose Confirm.");
        if (m_Synth == null)
        {
            SetText("<b>Voice sample unavailable</b>\nVoice audio is not ready. Please restart setup.");
            return;
        }
        m_Synth.PreviewNeutralVoice(m_ChosenGender, option, success =>
        {
            if (request != m_PreviewRequest || m_Phase != Phase.ChoosingNeutralVoice) return;
            if (success) m_HeardVoice[option] = true;
            SetText(success
                ? "<b>Point at a voice to hear it.</b>\nPull the trigger to select, then choose Confirm."
                : $"<b>{name}'s sample is unavailable.</b>\nPoint away and back to retry, or try the other voice.");
            RefreshVoiceSelection();
        });
    }

    /// <summary>Trigger selects a card; only Confirm commits it to the study session.</summary>
    public void SelectNeutralVoiceOption(int option)
    {
        if (m_Phase != Phase.ChoosingNeutralVoice || option < 0 || option > 1) return;
        m_SelectedVoiceOption = option;
        if (!m_HeardVoice[option] && (m_PreviewOption != option || m_Synth == null || !m_Synth.IsBusy))
            PreviewNeutralVoiceOption(option);
        RefreshVoiceSelection();
    }

    bool CanConfirmNeutralVoice() => m_SelectedVoiceOption >= 0 && m_HeardVoice[m_SelectedVoiceOption] &&
        m_Synth != null && !m_Synth.IsBusy;

    void RefreshVoiceSelection()
    {
        for (int i = 0; i < m_VoiceBorders.Length; i++)
            if (m_VoiceBorders[i] != null) m_VoiceBorders[i].enabled = i == m_SelectedVoiceOption;
        if (m_VoiceActionButton == null) return;
        string label = m_SelectedVoiceOption < 0 ? "Back" : "Confirm";
        m_VoiceActionButton.name = label;
        m_VoiceActionButton.GetComponentInChildren<TextMeshProUGUI>().text = label;
        m_VoiceActionButton.interactable = m_SelectedVoiceOption < 0 || CanConfirmNeutralVoice();
    }

    public void ConfirmNeutralVoice()
    {
        if (m_Phase != Phase.ChoosingNeutralVoice || !CanConfirmNeutralVoice()) return;
        m_PreviewRequest++;
        SessionConfig.SelectNeutralVoice(m_ChosenGender, m_SelectedVoiceOption);
        SessionConfig.Voice = VoiceCondition.Generic;
        SessionConfig.SelfSimilarEnrollmentPending = false;
        Debug.Log($"{k_Tag} Neutral {SessionConfig.NeutralProfile} / {SessionConfig.NeutralVoiceName} voice selected");
        try { SessionConfig.ConfigureVoiceBlocks(replaceNeutralSelection: true); }
        catch (System.Exception e) { ShowEnrollmentFailure(e.Message); return; }
        SelectSelfSimilar();
    }

    public void SelectSelfSimilar()
    {
        if (!m_PerspectiveChosen || m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        SessionConfig.LockPerspective();
        if (!SessionConfig.VoiceBlocksEnabled)
        {
            try { SessionConfig.ConfigureVoiceBlocks(); }
            catch (System.Exception e) { ShowEnrollmentFailure(e.Message); return; }
        }
        SessionConfig.Voice = VoiceCondition.SelfSimilar;
        Debug.Log($"{k_Tag} Self-similar voice selected — enrolling");
        m_Phase = Phase.Enrolling;

        // Silence any queued intro so it can't play over — or leak into —
        // the microphone recording.
        if (m_Synth != null) m_Synth.Stop();

        // Never reuse a device-wide clone: its participant ownership is unknown.
        SessionConfig.SelfSimilarVoiceId = "";
        SessionConfig.SelfSimilarEnrollmentPending = true;
        if (m_Enrollment == null)
        {
            ShowEnrollmentFailure("Voice enrollment unavailable.");
            return;
        }

        StartCoroutine(EnrollFlow());
    }

    IEnumerator EnrollFlow()
    {
        SetText($"<b>Voice recording instructions</b>\nListen first. Wait for the tone before reading.\n\n“{k_ReadingPassage}”");
        yield return m_Synth.SpeakRecordingIntroduction();
        if (!string.IsNullOrEmpty(m_Synth.LastError))
        {
            ShowEnrollmentFailure("Could not play the recording instructions.");
            yield break;
        }

        m_Enrollment.RecordAndClone(
            m_RecordSeconds,
            onState: s =>
            {
                if (s == VoiceEnrollment.State.Recording)
                    SetText($"<b>● Recording — read the commands aloud</b>\nUse your natural guiding voice. Select Finish recording when done.\n\n“{k_ReadingPassage}”");
                if (s == VoiceEnrollment.State.Cloning) SetText("<b>Creating your voice…</b>\nOne moment.");
            },
            onDone: _ =>
            {
                SessionConfig.SelfSimilarEnrollmentPending = false;
                StartCoroutine(PrepareVoices());
            },
            onError: err =>
            {
                Debug.LogWarning($"{k_Tag} Enrollment failed: {err}");
                ShowEnrollmentFailure("Could not create your voice.");
            },
            beforeRecording: RecordingStartTone);
    }

    IEnumerator RecordingStartTone()
    {
        SetText($"<b>Wait for the tone, then begin reading.</b>\n\n“{k_ReadingPassage}”");
        const int sampleRate = 22050;
        const float duration = 0.2f;
        var samples = new float[(int)(sampleRate * duration)];
        for (int i = 0; i < samples.Length; i++)
        {
            float fade = Mathf.Min(1f, Mathf.Min(i, samples.Length - 1 - i) / (sampleRate * 0.01f));
            samples[i] = 0.2f * fade * Mathf.Sin(2f * Mathf.PI * 660f * i / sampleRate);
        }
        var clip = AudioClip.Create("RecordingStartTone", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.7f;
        source.clip = clip;
        try
        {
            source.Play();
            while (source.isPlaying) yield return null;
        }
        finally
        {
            source.Stop();
            Destroy(source);
            Destroy(clip);
        }
    }

    IEnumerator PrepareVoices()
    {
        m_Phase = Phase.Preparing;
        if (m_Text == null) BuildPanel();
        SetText("<b>Processing voice…</b>\nPreparing voice checks and the first practice round. Please wait.");
        yield return m_Synth.SpeakProcessingStatus(false);
        bool ready = false;
        yield return m_Synth.PrepareLibraries(VoiceAssistantController.StarterPhrases(), SetText,
            ok => ready = ok, VoiceAssistantController.PhraseLibrary());
        if (!ready)
        {
            m_Phase = Phase.PreparationFailed;
            if (m_Synth.ProviderPolicyBlocked)
            {
                SetText($"<b>Voice provider blocked a phrase</b>\n{m_Synth.PreparationStage}\nThe provider rejected study text under its content policy. Ask the researcher to resolve this with the provider. Recording again will not resolve this text rejection.\n\nSelect Cancel session below.");
                yield break;
            }
            string reason = m_Synth.LastError ?? "No error details were returned.";
            if (reason.Length > 240) reason = reason.Substring(0, 240) + "…";
            reason = reason.Replace("<", "‹").Replace(">", "›");
            SetText($"<b>Audio preparation failed</b>\n{m_Synth.PreparationStage}\n{reason}\n\nRetry audio preparation or record your voice again.");
            yield break;
        }
        SetText("<b>Voice setup ready</b>\nNext, check the audio. Remaining prompts load in the background.");
        yield return m_Synth.SpeakProcessingStatus(true);
        m_Phase = Phase.CheckingNeutral;
        SessionConfig.Voice = VoiceCondition.Generic;
        SetText($"<b>Check {SessionConfig.NeutralVoiceName} audio</b>\nConfirm voice, clarity and comfortable volume.\nWhen the sample finishes, point at Accept audio and press the trigger.");
        m_Synth.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
        m_Synth.StartBackgroundLoading(VoiceAssistantController.BackgroundPhrases());
    }

    public void ConfirmAudioSample()
    {
        if (m_Synth == null || m_Synth.IsBusy || !string.IsNullOrEmpty(m_Synth.LastError)) return;
        if (m_Phase == Phase.CheckingNeutral)
        {
            m_Phase = Phase.CheckingSelf;
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            SetText("<b>Check your self-similar audio</b>\nConfirm voice identity, clarity and volume.\nWhen the sample finishes, point at Accept audio and press the trigger.");
            m_Synth.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
        }
        else if (m_Phase == Phase.CheckingSelf)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(SessionConfig.ParticipantPath, "voice-readiness-v1.txt"),
                $"{System.DateTime.UtcNow:O} both_audio_samples_accepted profile={SessionConfig.NeutralProfile} neutral_name={SessionConfig.NeutralVoiceName} neutral_id={SessionConfig.NeutralVoiceId} neutral_option={SessionConfig.NeutralVoiceOption} order={SessionConfig.VoiceOrder} perspective_policy=neutral_external_self_first_person perspective_version={SessionConfig.PerspectiveVersion}"); }
            catch (System.Exception) { SetText("Could not save audio readiness. Check device storage, then accept again."); return; }
            SessionConfig.ApplyRoundVoice(0);
            Finish($"{SessionConfig.ParticipantId}: both voices ready.\nWording: neutral external / self-similar first person\nOrder: {SessionConfig.VoiceOrder.Replace("_", " ")}");
        }
    }

    public void CancelSetup()
    {
        if (m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        m_Phase = Phase.Cancelled;
        m_Synth?.Stop();
        if (m_CanvasGO != null) Destroy(m_CanvasGO);
        GetComponent<FindObjectGameManager>()?.TechnicalStop("setup_cancelled");
    }

    void ShowEnrollmentFailure(string message)
    {
        SessionConfig.SelfSimilarVoiceId = "";
        SessionConfig.SelfSimilarEnrollmentPending = false;
        m_Phase = Phase.EnrollmentFailed;
        if (m_Text == null) BuildPanel();
        SetText(message + "\n\nRecord again or cancel the session.");
    }

    void Finish(string message)
    {
        m_Phase = Phase.Done;
        RefreshChoices();
        SelectionCompleted?.Invoke();
        SetText(message + (GetComponent<FindObjectGameManager>()?.RotationalBetaEnabled == true
            ? "\n\nStay seated. The center-and-begin step is next."
            : "\n\nTap a nearby surface to begin."));
        // Leave the confirmation up briefly, then remove the panel.
        if (m_CanvasGO != null) Destroy(m_CanvasGO, 3.0f);
        enabled = false;
    }

    // --- XR controller input (headset) ---
    bool ReadTrigger()  => ReadButton(CommonUsages.triggerButton);
    bool ReadPrimary()  => ReadButton(CommonUsages.primaryButton);

    bool ReadButton(InputFeatureUsage<bool> usage)
    {
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, m_Devices);
        foreach (var d in m_Devices)
            if (d.TryGetFeatureValue(usage, out bool pressed) && pressed) return true;
        return false;
    }

}
