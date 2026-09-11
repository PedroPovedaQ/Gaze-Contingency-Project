using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// On-entry launch-mode picker: choose Neutral (male/female ElevenLabs) or
/// Self-Similar (Voxtral clone of the participant) voice. Shown once at startup
/// as a small world-space panel in front of the camera.
///
/// Selection:
///   Trigger  or keyboard [1]  -> Neutral voice   (SessionConfig.Voice = Generic)
///   A/Primary or keyboard [2] -> Self-Similar     (records + clones, then continues)
///
/// The researcher may also just call SelectNeutralMale()/SelectNeutralFemale()/SelectSelfSimilar() or set
/// the default in the inspector (m_DefaultMode) and skip the panel via m_AutoConfirmDefault.
/// </summary>
public class VoiceModeSelector : MonoBehaviour
{
    const string k_Tag = "[VoiceMode]";

    // Short task commands read aloud during enrollment at a natural guiding pace.
    const string k_ReadingPassage =
        "Locate the red sphere. Look toward the upper shelf. " +
        "Move your gaze slowly to the right. Check the blue cube near the center. " +
        "Compare each object's color and shape. Scan the lower row from left to right. " +
        "Look beside the purple cylinder. Focus on the target and hold your gaze steady. " +
        "Select the matching object. Take a short pause. Get ready for the next round.";

    [Header("Optional overrides")]
    [SerializeField] VoiceCondition m_DefaultMode = VoiceCondition.Generic;
    [SerializeField] bool m_AutoConfirmDefault = false; // skip the panel, use m_DefaultMode
    [SerializeField] int m_RecordSeconds = 40;

    [SerializeField] NeutralVoiceProfile m_DefaultNeutralProfile = NeutralVoiceProfile.Female;

    public event System.Action SelectionCompleted;
    public bool IsComplete => m_Phase == Phase.Done;

    enum Phase { WaitingToStart, Choosing, ChoosingNeutral, Enrolling, EnrollmentFailed, Preparing, PreparationFailed, CheckingNeutral, CheckingSelf, Cancelled, Done }
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
    bool m_FirstPoll = true;
    bool m_PrevTrigger;
    bool m_PrevPrimary;
    bool m_PrevSecondary;

    public void Initialize(VoiceEnrollment enrollment, VoiceSynthesizer synth = null)
    {
        m_Enrollment = enrollment;
        m_Synth = synth;
        BuildPanel();
        m_Phase = Phase.WaitingToStart;
        SetText("<b>Ready to begin?</b>\n\nDismiss any headset notices first.\nRelease the controller buttons, then select\n<b>Start setup</b> below.");
    }

    public void StartSetup()
    {
        if (m_Phase != Phase.WaitingToStart || !m_StartArmed || !Application.isFocused) return;
        m_StartArmed = false;
        m_Phase = Phase.Choosing;
        m_FirstPoll = true;
        m_SetupInputAfter = Time.realtimeSinceStartup + 0.75f;
        m_FinishRecordingButton.gameObject.SetActive(false);
        if (m_AutoConfirmDefault)
        {
            if (m_DefaultMode == VoiceCondition.SelfSimilar) SelectSelfSimilar();
            else SelectNeutral(m_DefaultNeutralProfile);
            return;
        }

        SelectGeneric();
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
        label.text = "Finish recording · Trigger / 1";
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
        m_FirstPoll = true;
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
            m_ActionLabel.text = "Finish recording · Trigger / 1";
        }
        if (!Application.isFocused || Time.realtimeSinceStartup < m_SetupInputAfter)
        { m_FirstPoll = true; return; }
        if (m_Phase != Phase.Choosing && m_Phase != Phase.ChoosingNeutral && m_Phase != Phase.Enrolling &&
            m_Phase != Phase.EnrollmentFailed && m_Phase != Phase.PreparationFailed &&
            m_Phase != Phase.CheckingNeutral && m_Phase != Phase.CheckingSelf) return;

        bool trig = ReadTrigger();
        bool prim = ReadPrimary();
        bool secondary = ReadButton(CommonUsages.secondaryButton);

        // Ignore any button already held when the panel first appears, so a
        // trigger held from the previous screen can't auto-select.
        if (m_FirstPoll)
        {
            m_PrevTrigger = trig; m_PrevPrimary = prim; m_PrevSecondary = secondary; m_FirstPoll = false;
            return;
        }

        bool trigEdge = trig && !m_PrevTrigger;   // rising edge only
        bool primEdge = prim && !m_PrevPrimary;
        bool secondaryEdge = secondary && !m_PrevSecondary;
        m_PrevSecondary = secondary;
        m_PrevTrigger = trig; m_PrevPrimary = prim;

        if (m_Phase == Phase.Enrolling)
        {
            if (trigEdge || KeyPressed(1)) FinishRecording();
            return;
        }

        if (m_Phase == Phase.PreparationFailed)
        {
            if (m_Synth.ProviderPolicyBlocked)
            {
                if (primEdge || KeyPressed(2)) CancelSetup();
                return;
            }
            if (trigEdge || KeyPressed(1)) StartCoroutine(PrepareVoices());
            else if (primEdge || KeyPressed(2)) SelectSelfSimilar();
            return;
        }
        if (m_Phase == Phase.CheckingNeutral || m_Phase == Phase.CheckingSelf)
        {
            if (trigEdge || KeyPressed(1)) ConfirmAudioSample();
            else if (primEdge || KeyPressed(2)) m_Synth.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
            else if (secondaryEdge || KeyPressed(3)) SelectGeneric();
            return;
        }

        if (m_Phase == Phase.EnrollmentFailed)
        {
            if (trigEdge || KeyPressed(1)) SelectSelfSimilar();
            else if (primEdge || KeyPressed(2)) CancelSetup();
            return;
        }

        if (m_Phase == Phase.ChoosingNeutral)
        {
            if (trigEdge || KeyPressed(1)) SelectNeutralMale();
            else if (primEdge || KeyPressed(2)) SelectNeutralFemale();
            return;
        }

        if (trigEdge || KeyPressed(1)) { SelectGeneric(); return; }
        if (primEdge || KeyPressed(2)) { SelectSelfSimilar(); return; }
    }

    public void SelectGeneric()
    {
        if (m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        m_Synth?.Stop();
        m_Phase = Phase.ChoosingNeutral;
        m_FirstPoll = true;
        SetText("<b>Neutral voice</b>\n\nPull <b>Trigger</b> (or press 1): Male\n" +
                "Press <b>A / X</b> (or press 2): Female");
    }

    public void SelectNeutralMale() => SelectNeutral(NeutralVoiceProfile.Male);
    public void SelectNeutralFemale() => SelectNeutral(NeutralVoiceProfile.Female);

    void SelectNeutral(NeutralVoiceProfile profile)
    {
        if (m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
        m_Synth?.Stop();
        SessionConfig.NeutralProfile = profile;
        SessionConfig.Voice = VoiceCondition.Generic;
        SessionConfig.SelfSimilarEnrollmentPending = false;
        Debug.Log($"{k_Tag} Neutral {profile} voice selected");
        try { SessionConfig.ConfigureVoiceBlocks(); }
        catch (System.Exception e) { ShowEnrollmentFailure(e.Message); return; }
        SelectSelfSimilar();
    }

    public void SelectSelfSimilar()
    {
        if (m_Phase == Phase.WaitingToStart || m_Phase == Phase.Done || m_Phase == Phase.Enrolling || m_Phase == Phase.Preparing) return;
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
            beforeRecording: RecordingCountdownAndTone);
    }

    IEnumerator RecordingCountdownAndTone()
    {
        for (int c = 3; c > 0; c--)
        {
            SetText($"<b>Wait for the tone — recording starts in {c}…</b>\n\n“{k_ReadingPassage}”");
            yield return new WaitForSeconds(1f);
        }
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
        SetText("<b>Processing voice…</b>\nPreparing the audio for both voices. Please wait.");
        yield return m_Synth.SpeakProcessingStatus(false);
        bool ready = false;
        yield return m_Synth.PrepareLibraries(VoiceAssistantController.PhraseLibrary(), SetText, ok => ready = ok);
        if (!ready)
        {
            m_Phase = Phase.PreparationFailed; m_FirstPoll = true;
            if (m_Synth.ProviderPolicyBlocked)
            {
                SetText($"<b>Voice provider blocked a phrase</b>\n{m_Synth.PreparationStage}\nThe provider rejected study text under its content policy. Ask the researcher to resolve this with the provider. Recording again will not resolve this text rejection.\n\nA / X / 2: Cancel session");
                yield break;
            }
            string reason = m_Synth.LastError ?? "No error details were returned.";
            if (reason.Length > 240) reason = reason.Substring(0, 240) + "…";
            reason = reason.Replace("<", "‹").Replace(">", "›");
            SetText($"<b>Audio preparation failed</b>\n{m_Synth.PreparationStage}\n{reason}\n\nTrigger / 1: Retry preparation\nA / X / 2: Record voice again");
            yield break;
        }
        SetText("<b>Voice processing complete</b>\nBoth voices are ready. Next, check the audio.");
        yield return m_Synth.SpeakProcessingStatus(true);
        m_Phase = Phase.CheckingNeutral;
        SessionConfig.Voice = VoiceCondition.Generic;
        m_FirstPoll = true;
        SetText($"<b>Check neutral {SessionConfig.NeutralProfile.ToString().ToLowerInvariant()} audio</b>\nConfirm voice, clarity and comfortable volume.\nTrigger / 1: Accept\nA / X / 2: Replay\nB / Y / 3: Restart voice setup");
        m_Synth.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
    }

    public void ConfirmAudioSample()
    {
        if (m_Synth == null || m_Synth.IsBusy || !string.IsNullOrEmpty(m_Synth.LastError)) return;
        if (m_Phase == Phase.CheckingNeutral)
        {
            m_Phase = Phase.CheckingSelf; m_FirstPoll = true;
            SessionConfig.Voice = VoiceCondition.SelfSimilar;
            SetText("<b>Check your self-similar audio</b>\nConfirm voice identity, clarity and volume.\nTrigger / 1: Accept\nA / X / 2: Replay\nB / Y / 3: Restart voice setup");
            m_Synth.Speak(VoiceAssistantController.AudioCheckLine, "audio_check");
        }
        else if (m_Phase == Phase.CheckingSelf)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(SessionConfig.ParticipantPath, "voice-readiness-v1.txt"),
                $"{System.DateTime.UtcNow:O} both_audio_samples_accepted profile={SessionConfig.NeutralProfile} order={SessionConfig.VoiceOrder}"); }
            catch (System.Exception) { SetText("Could not save audio readiness. Check device storage, then accept again."); return; }
            SessionConfig.ApplyRoundVoice(0);
            Finish($"{SessionConfig.ParticipantId}: both voices ready.\nOrder: {SessionConfig.VoiceOrder.Replace("_", " ")}");
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
        m_FirstPoll = true;
        if (m_Text == null) BuildPanel();
        SetText(message + "\n\nTrigger / 1: Retry\nA / X / 2: Cancel session");
    }

    void Finish(string message)
    {
        m_Phase = Phase.Done;
        SelectionCompleted?.Invoke();
        SetText(message + "\n\nTap a nearby surface to begin.");
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

    // --- Keyboard fallback for editor testing ---
    bool KeyPressed(int digit)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return false;
        return digit == 1 ? kb.digit1Key.wasPressedThisFrame : digit == 2 ? kb.digit2Key.wasPressedThisFrame : kb.digit3Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(digit == 1 ? KeyCode.Alpha1 : digit == 2 ? KeyCode.Alpha2 : KeyCode.Alpha3);
#endif
    }
}
