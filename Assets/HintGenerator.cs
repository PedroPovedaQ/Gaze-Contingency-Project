using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Gaze-relative left/right guidance, interrupted by a debounced target-wall entry cue.</summary>
public class HintGenerator : MonoBehaviour
{
    public const string AreaCorrectionPhrase = DirectionalHintPolicy.AreaPhrase;
    const float k_TipInterval = 4f;
    const float k_FirstTipDelay = 2f;
    readonly GazeZoneEntryGate m_ZoneEntry = new GazeZoneEntryGate();
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
        // A reproducible starting phrase per participant/trial; each direction then
        // cycles all four forms before repeating. Actual spoken text is logged.
        int firstVariant = (ChallengeSet.ScheduleSeed % 4 + (m_GameManager != null ? m_GameManager.CurrentObjectiveIndex : 0)) % 4;
        m_LeftVariant = m_RightVariant = firstVariant;
        m_CurrentHint = null;
        m_LastTipTime = Time.time - (k_TipInterval - k_FirstTipDelay);
        m_TipsSuppressed = false;
    }

    // Wrong selections keep the same target and gaze-based guidance.
    public void OnWrongCapture() { }

    public void CancelPending()
    {
        m_TipsSuppressed = true;
        m_ZoneEntry.Reset();
        m_CurrentHint = null;
        m_Voice?.InterruptIfAbout("tip");
    }

    void Update()
    {
        if (m_Voice == null || m_GameManager == null) return;
        if (m_TipsSuppressed || !m_GameManager.SearchActive ||
            m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;

        bool inside = IsGazeInTargetPlane(out bool valid);
        string desired = valid ? (inside ? AreaCorrectionPhrase : GetDirection()) : null;
        // Stop stale directional requests/playback on a reversal or tracking loss.
        // Entry into the right area uses the short fade below instead of waiting for speech to finish.
        if (m_CurrentHint != null && desired != m_CurrentHint && (!valid || !inside))
        {
            m_Voice.InterruptIfAbout("tip");
            m_CurrentHint = null;
        }
        if (m_ZoneEntry.Update(valid, inside, Time.timeAsDouble) &&
            m_Voice.TryAreaCorrection(AreaCorrectionPhrase, AreaCorrectionStillRelevant))
        {
            m_ZoneEntry.MarkSpoken(Time.timeAsDouble);
            m_CurrentHint = AreaCorrectionPhrase;
            m_LastTipTime = Time.time;
            return;
        }
        if (!valid || inside || desired == null || m_Voice.IsBusy || Time.time - m_LastTipTime < k_TipInterval) return;
        m_LastTipTime = Time.time;
        m_CurrentHint = desired;
        int variant = desired == DirectionalHintPolicy.LeftPhrase ? m_LeftVariant++ : m_RightVariant++;
        string phrase = DirectionalHintPolicy.Variant(desired, variant);
        m_Voice.Speak(phrase, "tip");
        Debug.Log($"[HintGen] [{DirectionalHintPolicy.Version}] {phrase}");
    }

    string GetDirection()
    {
        if (!TryGetTargetInfo(out var target)) return null;
        // Aim toward the center of the target wall, not the particular object's horizontal slot.
        Vector3 normal = Quaternion.Euler(0, m_GameManager.SeatedForwardYaw + target.planeAzimuth, 0) * Vector3.forward;
        Vector3 origin = m_GameManager.SeatedOrigin;
        Vector3 targetOffset = target.transform.position - origin;
        Vector3 toWall = origin + normal * Vector3.Dot(targetOffset, normal) - m_GazeInteractor.transform.position;
        Vector3 gaze = m_GazeInteractor.transform.forward;
        return DirectionalHintPolicy.Direction(gaze.x, gaze.z, toWall.x, toWall.z);
    }

    bool AreaCorrectionStillRelevant() => !m_TipsSuppressed && m_GameManager != null &&
        m_GameManager.SearchActive && IsGazeInTargetPlane(out _);

    bool IsGazeInTargetPlane(out bool valid)
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
        valid = true;
        // Intersect the target's wall, including empty space between its objects.
        Quaternion rotation = Quaternion.Euler(0, m_GameManager.SeatedForwardYaw + target.planeAzimuth, 0);
        Vector3 normal = rotation * Vector3.forward;
        Vector3 direction = m_GazeInteractor.transform.forward;
        float denominator = Vector3.Dot(direction, normal);
        if (denominator <= 0.0001f) return false;
        float distance = Vector3.Dot(target.transform.position - m_GazeInteractor.transform.position, normal) / denominator;
        if (distance <= 0 || distance > 10) return false;
        Vector3 hit = m_GazeInteractor.transform.position + direction * distance;
        return m_GameManager.IsInsideSearchPlane(hit, target.planeId);
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
