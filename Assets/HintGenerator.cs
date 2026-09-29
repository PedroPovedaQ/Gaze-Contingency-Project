using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Gaze-relative left/right guidance, interrupted by a debounced target-wall entry cue.</summary>
public class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const float k_TipInterval = 4f;
    readonly GazeZoneEntryGate m_ZoneEntry = new GazeZoneEntryGate();
    readonly GazeReturnCueGate m_ReturnCue = new GazeReturnCueGate();
    string m_ReturnDirection;
    readonly List<InputDevice> m_EyeDevices = new List<InputDevice>();
    InputDevice m_EyeDevice;
    float m_NextEyeSearch;
    float m_LastTipTime;
    bool m_TipsSuppressed = true;
    string m_CurrentHint;
    int m_LeftVariant, m_RightVariant;
    VoiceSynthesizer m_Voice;
    FindObjectGameManager m_GameManager;
    XRBaseInputInteractor m_GazeInteractor;

    // Preserve the controller's existing initialization API.
    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null)
    {
        m_Voice = voice;
    }

    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null) m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
    }

    public void OnNewObjective()
    {
        m_ZoneEntry.Reset();
        m_ReturnCue.Reset();
        m_ReturnDirection = null;
        // A reproducible starting phrase per participant/trial; each direction then
        // cycles its allowed forms before repeating. Actual spoken text is logged.
        int firstVariant = (ChallengeSet.ScheduleSeed % 4 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 4;
        m_LeftVariant = m_RightVariant = firstVariant;
        m_CurrentHint = null;
        // The first directional hint is eligible on the first valid search frame.
        // Subsequent hints still use the normal spacing after each spoken cue.
        m_LastTipTime = Time.time - k_TipInterval;
        m_TipsSuppressed = false;
    }

    // Wrong selections keep the same target and gaze-based guidance.
    public void OnWrongCapture() { }

    public void CancelPending()
    {
        m_TipsSuppressed = true;
        m_ZoneEntry.Reset();
        m_ReturnCue.Reset();
        m_ReturnDirection = null;
        m_CurrentHint = null;
        m_Voice?.InterruptIfAbout("tip");
    }

    void Update()
    {
        if (m_Voice == null || m_GameManager == null) return;
        if (m_TipsSuppressed || !m_GameManager.SearchActive ||
            m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;

        bool inside = IsGazeInTargetSector(out bool valid);
        bool entryReady = m_ZoneEntry.Update(valid, inside, Time.timeAsDouble);
        bool returnReady = m_ReturnCue.Update(valid, inside, Time.timeAsDouble);
        bool far = false;
        string desired = valid ? (inside ? AreaCorrectionPhrase : GetDirection(out far)) : null;
        bool returning = m_CurrentHint == DirectionalHintPolicy.ReturnPhrase;
        // A return cue is stale once the area is regained or the return direction changes.
        bool stale = !valid || (returning ? inside || desired != m_ReturnDirection : !inside && desired != m_CurrentHint);
        if (m_CurrentHint != null && stale)
        {
            m_Voice.InterruptIfAbout("tip");
            m_CurrentHint = null;
        }
        if (entryReady && m_Voice.TryAreaCorrection(AreaCorrectionPhrase, AreaCorrectionStillRelevant, () =>
            {
                // A gaze change during the fade must not consume the six-second cooldown.
                m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
                m_LastTipTime = Time.time;
            }))
        {
            m_CurrentHint = AreaCorrectionPhrase;
            return;
        }
        if (returnReady && desired != null && m_Voice.TryAreaCorrection(DirectionalHintPolicy.ReturnPhrase,
            () => ReturnCueStillRelevant(desired), () =>
            {
                m_ReturnCue.MarkSpoken(Time.timeAsDouble);
                m_LastTipTime = Time.time;
            }, "gaze_overshoot_return"))
        {
            m_ReturnDirection = desired;
            m_CurrentHint = DirectionalHintPolicy.ReturnPhrase;
            return;
        }
        // Even during the entry cue cooldown, don't keep saying left/right after arrival.
        if (m_ZoneEntry.InsideStable && m_CurrentHint != AreaCorrectionPhrase)
        {
            m_Voice.InterruptIfAbout("tip");
            m_CurrentHint = null;
        }
        if (!valid || inside || desired == null || m_ReturnCue.IsDebouncingExit ||
            m_Voice.IsBusy || Time.time - m_LastTipTime < k_TipInterval) return;
        m_LastTipTime = Time.time;
        m_CurrentHint = desired;
        int variant = desired == DirectionalHintPolicy.LeftPhrase ? m_LeftVariant++ : m_RightVariant++;
        string phrase = DirectionalHintPolicy.Variant(desired, variant, SessionConfig.Voice == VoiceCondition.SelfSimilar, far);
        m_Voice.Speak(phrase, "tip");
        Debug.Log($"[HintGen] [{DirectionalHintPolicy.Version}] {phrase}");
    }

    string GetDirection(out bool far)
    {
        far = false;
        if (!TryGetTargetInfo(out var target)) return null;
        Vector3 toTarget = target.transform.position - m_GazeInteractor.transform.position;
        Vector3 gaze = m_GazeInteractor.transform.forward;
        return DirectionalHintPolicy.Direction(gaze.x, gaze.z, toTarget.x, toTarget.z, out far);
    }

    bool ReturnCueStillRelevant(string direction)
    {
        if (m_TipsSuppressed || m_GameManager == null || !m_GameManager.SearchActive ||
            !m_ReturnCue.IsPending(Time.timeAsDouble)) return false;
        bool inside = IsGazeInTargetSector(out bool valid);
        return valid && !inside && GetDirection(out _) == direction;
    }

    bool AreaCorrectionStillRelevant() => !m_TipsSuppressed && m_GameManager != null &&
        m_GameManager.SearchActive && IsGazeInTargetSector(out _);

    bool IsGazeInTargetSector(out bool valid)
    {
        valid = false;
        if (m_GameManager == null || !m_GameManager.RotationalBetaEnabled ||
            m_GazeInteractor == null || !m_GazeInteractor.isActiveAndEnabled || !Application.isFocused) return false;
        if (!m_EyeDevice.isValid && Time.time >= m_NextEyeSearch)
        {
            m_NextEyeSearch = Time.time + 1f;
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, m_EyeDevices);
            if (m_EyeDevices.Count > 0) m_EyeDevice = m_EyeDevices[0];
        }
        if (!m_EyeDevice.isValid || !m_EyeDevice.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked)
            return false;
        if (!TryGetTargetInfo(out var target) || target.planeId < 0) return false;
        return m_GameManager.IsGazeInSearchSector(m_GazeInteractor.transform.position,
            m_GazeInteractor.transform.forward, target.planeId, out valid);
    }

    bool TryGetTargetInfo(out SpawnableObjectInfo targetInfo)
    {
        targetInfo = null;
        if (m_GameManager == null) return false;
        var target = m_GameManager.CurrentTarget;
        if (string.IsNullOrEmpty(target.shape) || string.IsNullOrEmpty(target.color)) return false;
        foreach (var obj in m_GameManager.SpawnedObjects)
        {
            if (obj == null || !obj.activeSelf) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
            {
                targetInfo = info;
                return true;
            }
        }
        return false;
    }

    public static string[] AllPhrases() => DirectionalHintPolicy.AllPhrases();
}
