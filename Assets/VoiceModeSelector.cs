using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using TMPro;

/// <summary>
/// On-entry launch-mode picker: choose Standard (generic ElevenLabs) or
/// Self-Similar (Voxtral clone of the participant) voice. Shown once at startup
/// as a small world-space panel in front of the camera.
///
/// Selection:
///   Trigger  or keyboard [1]  -> Standard voice   (SessionConfig.Voice = Generic)
///   A/Primary or keyboard [2] -> Self-Similar     (records + clones, then continues)
///
/// The researcher may also just call SelectGeneric()/SelectSelfSimilar() or set
/// the default in the inspector (m_DefaultMode) and skip the panel via m_AutoConfirmDefault.
/// </summary>
public class VoiceModeSelector : MonoBehaviour
{
    const string k_Tag = "[VoiceMode]";

    // PlayerPrefs key: persists the Voxtral clone id so the voice survives restarts.
    const string k_VoiceIdPref = "selfsim_voice_id";

    // Passage the participant reads aloud during enrollment (~40 s, phonetically varied).
    const string k_ReadingPassage =
        "Hello, my name is the study participant, and I am reading this short passage " +
        "so the system can learn the sound of my voice. I enjoy quiet mornings, strong " +
        "coffee, and long walks when the weather is clear. The quick brown fox jumps over " +
        "the lazy dog, and she sells sea shells by the sea shore. Please keep reading at a " +
        "steady, natural pace until the recording finishes. Thank you for listening.";

    [Header("Optional overrides")]
    [SerializeField] VoiceCondition m_DefaultMode = VoiceCondition.Generic;
    [SerializeField] bool m_AutoConfirmDefault = false; // skip the panel, use m_DefaultMode
    [SerializeField] int m_RecordSeconds = 40;

    enum Phase { Choosing, Enrolling, Done }
    Phase m_Phase = Phase.Choosing;

    VoiceEnrollment m_Enrollment;
    VoiceSynthesizer m_Synth;
    Canvas m_Canvas;
    GameObject m_CanvasGO;
    TextMeshProUGUI m_Text;
    readonly List<InputDevice> m_Devices = new List<InputDevice>();
    bool m_FirstPoll = true;
    bool m_PrevTrigger;
    bool m_PrevPrimary;

    string m_SavedVoiceId = "";

    public void Initialize(VoiceEnrollment enrollment, VoiceSynthesizer synth = null)
    {
        m_Enrollment = enrollment;
        m_Synth = synth;

        // A clone saved on a previous run can be reused without re-recording.
        m_SavedVoiceId = PlayerPrefs.GetString(k_VoiceIdPref, "");

        if (m_AutoConfirmDefault)
        {
            if (m_DefaultMode == VoiceCondition.SelfSimilar) SelectSelfSimilar();
            else SelectGeneric();
            return;
        }

        BuildPanel();
        SetText("<b>Voice mode</b>\n\nPull <b>Trigger</b> (or press 1):  Standard voice\n" +
                "Press <b>A / X</b> (or press 2):  Use my voice");
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
        var rect = m_CanvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(700, 360);
        m_CanvasGO.transform.localScale = Vector3.one * 0.0011f;

        var bg = new GameObject("Bg");
        bg.transform.SetParent(m_CanvasGO.transform, false);
        var bgRect = bg.AddComponent<RectTransform>();
        bgRect.sizeDelta = new Vector2(700, 360);
        bg.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.82f);

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(m_CanvasGO.transform, false);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.sizeDelta = new Vector2(660, 320);
        m_Text = textGO.AddComponent<TextMeshProUGUI>();
        m_Text.alignment = TextAlignmentOptions.Center;
        m_Text.fontSize = 34;
        m_Text.color = new Color(0.9f, 0.97f, 1f, 1f);
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

        if (m_Phase != Phase.Choosing) return;

        bool trig = ReadTrigger();
        bool prim = ReadPrimary();

        // Ignore any button already held when the panel first appears, so a
        // trigger held from the previous screen can't auto-select.
        if (m_FirstPoll)
        {
            m_PrevTrigger = trig; m_PrevPrimary = prim; m_FirstPoll = false;
            return;
        }

        bool trigEdge = trig && !m_PrevTrigger;   // rising edge only
        bool primEdge = prim && !m_PrevPrimary;
        m_PrevTrigger = trig; m_PrevPrimary = prim;

        if (trigEdge || KeyPressed(1)) { SelectGeneric(); return; }
        if (primEdge || KeyPressed(2)) { SelectSelfSimilar(); return; }
    }

    public void SelectGeneric()
    {
        SessionConfig.Voice = VoiceCondition.Generic;
        Debug.Log($"{k_Tag} Standard voice selected");
        Finish("Standard voice selected.");
    }

    public void SelectSelfSimilar()
    {
        SessionConfig.Voice = VoiceCondition.SelfSimilar;
        Debug.Log($"{k_Tag} Self-similar voice selected — enrolling");
        m_Phase = Phase.Enrolling;

        // Silence the generic intro ("Ava") so it can't play over — or leak into —
        // the microphone recording.
        if (m_Synth != null) m_Synth.Stop();

        if (m_Enrollment == null)
        {
            SessionConfig.Voice = VoiceCondition.Generic;
            SessionConfig.SelfSimilarEnrollmentPending = false;
            Finish("Voice enrollment unavailable — using standard voice.");
            return;
        }

        // Reuse a clone saved on a previous run — skip recording entirely.
        if (!string.IsNullOrEmpty(m_SavedVoiceId))
        {
            SessionConfig.SelfSimilarVoiceId = m_SavedVoiceId;
            SessionConfig.SelfSimilarEnrollmentPending = false;
            Debug.Log($"{k_Tag} Reusing saved clone voice_id={m_SavedVoiceId}");
            Finish("Using your saved voice.");
            return;
        }

        // Gate the synthesizer to silence (not generic) until the clone is ready.
        SessionConfig.SelfSimilarEnrollmentPending = true;
        StartCoroutine(EnrollFlow());
    }

    IEnumerator EnrollFlow()
    {
        // Show the passage and count down so the participant can start reading on cue.
        for (int c = 3; c > 0; c--)
        {
            SetText($"<b>Read this aloud — recording starts in {c}…</b>\n\n“{k_ReadingPassage}”");
            yield return new WaitForSeconds(1f);
        }
        SetText($"<b>● Recording — read this aloud:</b>\n\n“{k_ReadingPassage}”");

        m_Enrollment.RecordAndClone(
            m_RecordSeconds,
            onState: s =>
            {
                if (s == VoiceEnrollment.State.Cloning) SetText("<b>Creating your voice…</b>\nOne moment.");
            },
            onDone: _ =>
            {
                SessionConfig.SelfSimilarEnrollmentPending = false;
                // Persist the clone id so this voice survives an editor/app restart.
                PlayerPrefs.SetString(k_VoiceIdPref, SessionConfig.SelfSimilarVoiceId);
                PlayerPrefs.Save();
                Debug.Log($"{k_Tag} Clone saved. voice_id={SessionConfig.SelfSimilarVoiceId} " +
                          $"(persisted to PlayerPrefs '{k_VoiceIdPref}'; audio caches in tts_cache)");
                Finish("Your voice is ready.");
            },
            onError: err =>
            {
                Debug.LogWarning($"{k_Tag} Enrollment failed: {err}; falling back to standard voice");
                SessionConfig.Voice = VoiceCondition.Generic;
                SessionConfig.SelfSimilarEnrollmentPending = false;
                Finish("Could not create your voice — using standard voice.");
            });
    }

    void Finish(string message)
    {
        m_Phase = Phase.Done;
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
        return digit == 1 ? kb.digit1Key.wasPressedThisFrame : kb.digit2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(digit == 1 ? KeyCode.Alpha1 : KeyCode.Alpha2);
#endif
    }
}
