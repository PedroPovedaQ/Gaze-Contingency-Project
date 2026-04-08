using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Tip system — fires every 5 seconds. Two conditions:
///
/// GAZE-AWARE: Responds to the player's real-time gaze behavior with
/// temperature-only feedback (hot/cold). NEVER reveals the target's
/// location (shelf, direction, column).
///
/// GAZE-UNAWARE (control): Generic encouragement only. "Keep looking."
/// "Stay focused." No gaze reactivity, no location info, no
/// warmer/colder. Same tip regardless of what the player is doing.
///
/// Both conditions have identical information about WHERE the target is —
/// neither tells the player. The only variable is whether the agent
/// can see and respond to the player's gaze.
/// </summary>
public class HintGenerator : MonoBehaviour
{
    const string k_Tag = "[HintGen]";
    const int k_WarmthCold = 0;
    const int k_WarmthTrack = 1;
    const int k_WarmthVeryClose = 2;
    const float k_TipInterval = 5f;
    const float k_TipIntervalUnaware = 9f;
    const float k_FirstTipDelayUnaware = 5f;
    const float k_WrongCaptureWindow = 6f;
    const float k_HotMemoryWindow = 5.5f;
    const int k_UnawareNoRepeatWindow = 3;
    const float k_VeryCloseDistanceMeters = 0.16f;
    const float k_NearTargetDistanceMeters = 0.30f;
    const float k_NearTargetAngleDeg = 14f;
    const float k_NearTargetRayDistanceMeters = 0.18f;
    const float k_NearRayCandidateAngleDeg = 18f;
    const float k_NearRayCandidateDistanceMeters = 0.24f;
    const float k_MaxCandidateRayDistanceMeters = 8f;

    /// <summary>
    /// When true, tips respond to gaze with temperature-only feedback.
    /// When false, tips are generic encouragement only (control).
    /// Neither condition reveals the target's location.
    /// </summary>
    /// <summary>Resolved per round by FindObjectGameManager.</summary>
    public bool gazeAwareTips = true;

    AgentContext m_Context;
    VoiceSynthesizer m_Voice;
    GazeCoverageTracker m_CoverageTracker;
    FindObjectGameManager m_GameManager;
    XRBaseInputInteractor m_GazeInteractor;

    float m_ObjectiveStartTime;
    float m_LastTipTime;
    float m_WrongCaptureTime;
    float m_LastHotEvidenceTime;
    int m_LastPickIndex = -1;
    int m_LastAwareHintWarmth = -1;
    int m_NextAwareHintWarmth = -1;
    int m_LastAwareHintRow = -1;
    int m_LastAwareHintCol = -1;
    int m_NextAwareHintRow = -1;
    int m_NextAwareHintCol = -1;
    bool m_TipsSuppressed = true;

    // Keep recent gaze-state tracking for temperature classification.
    int m_LastGazedZone = -1;
    float m_TimeOnCurrentZone;
    bool m_WasGazingAtTarget;
    float m_LeftTargetTime;
    bool m_HasLoggedMode;
    bool m_LastResolvedMode;
    readonly Queue<string> m_RecentUnawarePhrases = new();

    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null)
    {
        m_Context = context;
        m_Voice = voice;
        m_CoverageTracker = coverageTracker;
    }

    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();

        // Use the same gaze interactor as the dwell/highlight system, but
        // resolve current gaze target via direct raycast (more stable than
        // relying on hovered list ordering).
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null)
            m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
    }

    public void OnNewObjective()
    {
        m_ObjectiveStartTime = Time.time;
        bool resolvedMode = m_GameManager != null ? m_GameManager.CurrentRoundGazeAware : gazeAwareTips;
        float tipInterval = resolvedMode ? k_TipInterval : k_TipIntervalUnaware;
        float firstDelay = resolvedMode ? k_TipInterval : k_FirstTipDelayUnaware;
        m_LastTipTime = Time.time - Mathf.Max(0f, tipInterval - firstDelay);
        m_WrongCaptureTime = 0f;
        m_LastHotEvidenceTime = -999f;
        m_LastPickIndex = -1;
        m_LastAwareHintWarmth = -1;
        m_NextAwareHintWarmth = -1;
        m_LastAwareHintRow = -1;
        m_LastAwareHintCol = -1;
        m_NextAwareHintRow = -1;
        m_NextAwareHintCol = -1;
        m_WasGazingAtTarget = false;
        m_LeftTargetTime = 0f;
        m_LastGazedZone = -1;
        m_TimeOnCurrentZone = 0f;
        m_TipsSuppressed = false;
    }

    public void OnWrongCapture()
    {
        m_WrongCaptureTime = Time.time;
    }

    public void CancelPending()
    {
        m_TipsSuppressed = true;
        m_WrongCaptureTime = 0f;
        m_LastHotEvidenceTime = -999f;
        m_LastAwareHintWarmth = -1;
        m_NextAwareHintWarmth = -1;
        m_LastAwareHintRow = -1;
        m_LastAwareHintCol = -1;
        m_NextAwareHintRow = -1;
        m_NextAwareHintCol = -1;
        m_WasGazingAtTarget = false;
        m_LeftTargetTime = 0f;
        m_LastGazedZone = -1;
        m_TimeOnCurrentZone = 0f;
    }

    void Update()
    {
        if (m_Voice == null || m_Context == null) return;
        if (m_GameManager == null || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;
        if (m_TipsSuppressed) return;

        // Resolve mode from the round state each frame so round boundary
        // switches (round 7 -> 8) are always applied even if a one-time
        // assignment is missed.
        bool resolvedMode = m_GameManager.CurrentRoundGazeAware;
        gazeAwareTips = resolvedMode;
        if (!m_HasLoggedMode || m_LastResolvedMode != resolvedMode)
        {
            Debug.Log($"{k_Tag} Mode sync: round={m_GameManager.CurrentObjectiveIndex + 1}, mode={(resolvedMode ? "AWARE" : "UNAWARE")}, condition={m_GameManager.CurrentRoundConditionLabel}");
            m_HasLoggedMode = true;
            m_LastResolvedMode = resolvedMode;
        }

        // Track gaze-near-target state for "go back" detection
        bool gazingAtTarget = IsGazingAtCurrentTarget();
        if (gazingAtTarget)
        {
            m_WasGazingAtTarget = true;
            m_LeftTargetTime = 0f;
        }
        else if (m_WasGazingAtTarget)
        {
            m_WasGazingAtTarget = false;
            m_LeftTargetTime = Time.time; // just looked away from target
        }

        // Track zone dwell for "you already checked there"
        int currentZone = GetPlayerCurrentZone();
        if (currentZone >= 0 && currentZone == m_LastGazedZone)
        {
            m_TimeOnCurrentZone += Time.deltaTime;
        }
        else if (currentZone >= 0)
        {
            m_LastGazedZone = currentZone;
            m_TimeOnCurrentZone = 0f;
        }

        if (m_Voice.IsBusy) return;

        float now = Time.time;
        float tipInterval = gazeAwareTips ? k_TipInterval : k_TipIntervalUnaware;
        if (now - m_LastTipTime < tipInterval) return;

        m_NextAwareHintWarmth = -1;
        m_NextAwareHintRow = -1;
        m_NextAwareHintCol = -1;
        string tip = gazeAwareTips ? EvaluateGazeAware(now) : EvaluateGazeUnaware(now);
        if (string.IsNullOrEmpty(tip)) return;

        m_LastTipTime = now;
        m_Voice.Speak(tip, "tip");
        if (gazeAwareTips && m_NextAwareHintWarmth >= 0)
        {
            m_LastAwareHintWarmth = m_NextAwareHintWarmth;
            m_LastAwareHintRow = m_NextAwareHintRow;
            m_LastAwareHintCol = m_NextAwareHintCol;
        }
        Debug.Log($"{k_Tag} [{(gazeAwareTips ? "AWARE" : "UNAWARE")}] \"{tip}\"");
    }

    // =====================================================================
    //  GAZE-AWARE — responds to behavior, never reveals location
    // =====================================================================

    string EvaluateGazeAware(float now)
    {
        if (!TryGetTargetInfo(out var targetInfo) || targetInfo == null)
            return PickAwareByWarmth(k_WarmthCold);

        bool hasLooked = TryGetGazedSpawnInfo(out var lookedInfo) && lookedInfo != null;
        if (hasLooked)
        {
            m_NextAwareHintRow = lookedInfo.shelfLevel;
            m_NextAwareHintCol = lookedInfo.shelfColumn;
        }

        // Hard guard: if we're confidently on the opposite bookcase, this must
        // be a cold response (never "right area").
        if (hasLooked && IsWrongBookcase(targetInfo, lookedInfo))
        {
            m_LastHotEvidenceTime = -999f;
            return PickAwareByWarmth(k_WarmthCold);
        }

        bool veryCloseNow = IsVeryCloseEvidence(targetInfo, lookedInfo, hasLooked);
        bool hotNow = veryCloseNow || HasNearEvidence(targetInfo, lookedInfo, hasLooked);
        if (hotNow)
        {
            m_LastHotEvidenceTime = now;
            return PickAwareByWarmth(veryCloseNow ? k_WarmthVeryClose : k_WarmthTrack);
        }

        // Smooth over momentary jitter/misses: if we had strong hot evidence
        // recently, keep the output hot across one tip interval.
        if ((now - m_LastHotEvidenceTime) <= k_HotMemoryWindow)
            return PickAwareByWarmth(k_WarmthTrack);

        // Wrong picks in aware mode should still be expressed as temperature only,
        // but never override current hot evidence.
        if (m_WrongCaptureTime > 0f && (now - m_WrongCaptureTime) < k_WrongCaptureWindow)
        {
            m_WrongCaptureTime = 0f;
            m_LastHotEvidenceTime = -999f;
            return PickAwareByWarmth(k_WarmthCold);
        }

        // Secondary fallback: if gaze ray is near target center despite no object
        // resolution this frame, still report hot.
        if (!hasLooked && IsGazeDirectionNearTarget(k_NearTargetAngleDeg, k_NearTargetRayDistanceMeters))
        {
            m_LastHotEvidenceTime = now;
            return PickAwareByWarmth(k_WarmthTrack);
        }

        return PickAwareByWarmth(k_WarmthCold);
    }

    // =====================================================================
    //  GAZE-UNAWARE (control) — generic encouragement, no gaze reactivity
    // =====================================================================

    string EvaluateGazeUnaware(float now)
    {
        if (m_WrongCaptureTime > 0f && (now - m_WrongCaptureTime) < k_WrongCaptureWindow)
        {
            m_WrongCaptureTime = 0f;
            return PickUnaware(k_GU_WrongCapture);
        }

        return PickUnaware(k_GU_General);
    }

    // =====================================================================
    //  Helpers
    // =====================================================================

    (int row, int col) GetPlayerPosition()
    {
        if (TryGetGazedSpawnInfo(out var info))
            return (info.shelfLevel, info.shelfColumn);
        return (-1, -1);
    }

    (int row, int col) GetTargetPosition()
    {
        if (m_GameManager == null) return (-1, -1);
        var target = m_GameManager.CurrentTarget;
        if (string.IsNullOrEmpty(target.shape) || string.IsNullOrEmpty(target.color)) return (-1, -1);
        foreach (var obj in m_GameManager.SpawnedObjects)
        {
            if (obj == null || !obj.activeSelf) continue;
            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info != null && info.shapeName == target.shape && info.colorName == target.color)
                return (info.shelfLevel, info.shelfColumn);
        }
        return (-1, -1);
    }

    // Keep for zone tracking in Update
    int GetPlayerCurrentZone()
    {
        var (row, _) = GetPlayerPosition();
        return row;
    }

    bool IsGazingAtCurrentTarget()
    {
        if (!TryGetGazedSpawnInfo(out var lookedInfo)) return false;
        if (m_GameManager == null) return false;
        var target = m_GameManager.CurrentTarget;
        if (string.IsNullOrEmpty(target.shape) || string.IsNullOrEmpty(target.color)) return false;
        return lookedInfo.shapeName == target.shape && lookedInfo.colorName == target.color;
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

    bool IsGazeDirectionNearTarget(float maxAngleDeg, float maxPerpDistanceMeters)
    {
        if (m_GazeInteractor == null) return false;
        if (!TryGetTargetInfo(out var targetInfo) || targetInfo == null) return false;

        Vector3 origin = m_GazeInteractor.transform.position;
        Vector3 forward = m_GazeInteractor.transform.forward;
        Vector3 toTarget = targetInfo.transform.position - origin;
        if (toTarget.sqrMagnitude <= 0.000001f) return true;
        float forwardDot = Vector3.Dot(forward, toTarget.normalized);
        if (forwardDot <= 0f) return false;
        float angle = Vector3.Angle(forward, toTarget);
        if (angle <= maxAngleDeg) return true;

        // Secondary check: distance from gaze ray to target point.
        // This helps when the ray is close to the target but intersects another object first.
        Vector3 toTargetOnRay = Vector3.Project(toTarget, forward.normalized);
        Vector3 perp = toTarget - toTargetOnRay;
        return perp.magnitude <= maxPerpDistanceMeters;
    }

    bool TryGetGazedSpawnInfo(out SpawnableObjectInfo info)
    {
        info = null;
        if (m_GazeInteractor == null) return false;

        var origin = m_GazeInteractor.transform.position;
        var direction = m_GazeInteractor.transform.forward.normalized;
        var layerMask = 1 << 8; // spawned searchable objects

        // Primary path: choose most directionally-aligned hovered spawned object.
        // This matches the same hover source the dwell system uses.
        var hovered = m_GazeInteractor.interactablesHovered;
        float bestScore = float.MaxValue;
        SpawnableObjectInfo best = null;
        for (int i = 0; i < hovered.Count; i++)
        {
            var interactable = hovered[i];
            if (interactable == null || interactable.transform == null) continue;
            var go = interactable.transform.gameObject;
            if (go == null || !go.activeInHierarchy || go.layer != 8) continue;

            var candidate = interactable.transform.GetComponentInParent<SpawnableObjectInfo>();
            if (candidate == null) continue;

            var toObj = candidate.transform.position - origin;
            float angle = Vector3.Angle(direction, toObj);
            float dist = toObj.magnitude;
            float score = angle + dist * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            info = best;
            return true;
        }

        // Secondary path: physics raycast directly from the gaze interactor.
        if (Physics.Raycast(origin, direction, out var hit, 10f, layerMask, QueryTriggerInteraction.Ignore))
        {
            info = hit.collider != null ? hit.collider.GetComponentInParent<SpawnableObjectInfo>() : null;
            if (info != null) return true;
        }

        // Final fallback: nearest spawned object to the gaze ray within a broad
        // cone. This handles near-target looks when the ray misses colliders.
        if (TryGetNearestToGazeRay(origin, direction, out var nearest))
        {
            info = nearest;
            return true;
        }

        return false;
    }

    bool IsWrongBookcase(SpawnableObjectInfo targetInfo, SpawnableObjectInfo lookedInfo)
    {
        if (targetInfo == null || lookedInfo == null) return false;
        if (targetInfo.shelfColumn < 0 || lookedInfo.shelfColumn < 0) return false;
        return targetInfo.shelfColumn != lookedInfo.shelfColumn;
    }

    bool IsVeryCloseEvidence(SpawnableObjectInfo targetInfo, SpawnableObjectInfo lookedInfo, bool hasLooked)
    {
        if (targetInfo == null) return false;
        if (IsGazingAtCurrentTarget()) return true;
        if (!hasLooked || lookedInfo == null) return false;
        if (IsWrongBookcase(targetInfo, lookedInfo)) return false;

        int rowDist = Mathf.Abs(targetInfo.shelfLevel - lookedInfo.shelfLevel);
        int colDist = Mathf.Abs(targetInfo.shelfColumn - lookedInfo.shelfColumn);
        float worldDist = Vector3.Distance(targetInfo.transform.position, lookedInfo.transform.position);
        return rowDist == 0 && colDist == 0 && worldDist <= k_VeryCloseDistanceMeters;
    }

    bool HasNearEvidence(SpawnableObjectInfo targetInfo, SpawnableObjectInfo lookedInfo, bool hasLooked)
    {
        if (targetInfo == null) return false;
        if (IsGazingAtCurrentTarget()) return true;

        if (hasLooked && lookedInfo != null)
        {
            if (IsWrongBookcase(targetInfo, lookedInfo)) return false;

            int rowDist = Mathf.Abs(targetInfo.shelfLevel - lookedInfo.shelfLevel);
            int colDist = Mathf.Abs(targetInfo.shelfColumn - lookedInfo.shelfColumn);
            float worldDist = Vector3.Distance(targetInfo.transform.position, lookedInfo.transform.position);

            bool isNearByGrid = rowDist <= 1 && colDist <= 1;
            bool isNearByDistance = worldDist <= k_NearTargetDistanceMeters;
            if (isNearByGrid || isNearByDistance)
                return true;
        }

        return !hasLooked && IsGazeDirectionNearTarget(k_NearTargetAngleDeg, k_NearTargetRayDistanceMeters);
    }

    bool TryGetNearestToGazeRay(Vector3 origin, Vector3 direction, out SpawnableObjectInfo nearest)
    {
        nearest = null;
        if (m_GameManager == null) return false;

        float bestScore = float.MaxValue;
        var spawned = m_GameManager.SpawnedObjects;
        for (int i = 0; i < spawned.Count; i++)
        {
            var obj = spawned[i];
            if (obj == null || !obj.activeSelf) continue;
            if (obj.layer != 8) continue;

            var info = obj.GetComponent<SpawnableObjectInfo>();
            if (info == null) continue;

            Vector3 toObj = info.transform.position - origin;
            float forwardDist = Vector3.Dot(direction, toObj);
            if (forwardDist <= 0f || forwardDist > k_MaxCandidateRayDistanceMeters) continue;

            float angle = Vector3.Angle(direction, toObj);
            if (angle > k_NearRayCandidateAngleDeg) continue;

            Vector3 perp = toObj - direction * forwardDist;
            float perpDist = perp.magnitude;
            if (perpDist > k_NearRayCandidateDistanceMeters) continue;

            float score = perpDist * 25f + angle + forwardDist * 0.01f;
            if (score < bestScore)
            {
                bestScore = score;
                nearest = info;
            }
        }

        return nearest != null;
    }

    string Pick(string[] pool)
    {
        int idx;
        if (pool.Length <= 1) { idx = 0; }
        else { do { idx = Random.Range(0, pool.Length); } while (idx == m_LastPickIndex); }
        m_LastPickIndex = idx;
        return pool[idx];
    }

    string PickUnaware(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "";

        // Prefer phrases not used in the recent unaware history window.
        string pick = "";
        for (int tries = 0; tries < 16; tries++)
        {
            string candidate = pool[Random.Range(0, pool.Length)];
            if (m_RecentUnawarePhrases.Contains(candidate)) continue;
            pick = candidate;
            break;
        }

        // Fallback: if all candidates are in the recent window, pick any random.
        if (string.IsNullOrEmpty(pick))
            pick = pool[Random.Range(0, pool.Length)];

        m_RecentUnawarePhrases.Enqueue(pick);
        while (m_RecentUnawarePhrases.Count > k_UnawareNoRepeatWindow)
            m_RecentUnawarePhrases.Dequeue();

        return pick;
    }

    string PickAwareByWarmth(int warmth)
    {
        m_NextAwareHintWarmth = warmth;
        bool lastAreaValid = m_LastAwareHintRow >= 0 && m_LastAwareHintCol >= 0;
        bool nextAreaValid = m_NextAwareHintRow >= 0 && m_NextAwareHintCol >= 0;
        bool areaChanged = lastAreaValid && nextAreaValid &&
                           (m_LastAwareHintRow != m_NextAwareHintRow || m_LastAwareHintCol != m_NextAwareHintCol);
        bool gettingColder = m_LastAwareHintWarmth >= 0 && warmth < m_LastAwareHintWarmth && areaChanged;
        if (gettingColder)
            return warmth <= k_WarmthCold ? Pick(k_GA_Colder_Cold) : Pick(k_GA_Colder_Track);

        if (warmth >= k_WarmthVeryClose) return Pick(k_GA_Hot_VeryClose);
        if (warmth == k_WarmthTrack) return Pick(k_GA_Hot_Track);
        return Pick(k_GA_Cold);
    }

    // =================================================================
    //  GAZE-AWARE TIPS — temperature only (hot/cold)
    // =================================================================

    // --- VERY CLOSE: tight on target area ---
    static readonly string[] k_GA_Hot_VeryClose =
    {
        "You're very close.",
        "Stay with this area.",
        "You're right where you need to be.",
        "Excellent. Stay with this area.",
        "Great, you're very close. Keep your focus here.",
        "You're nearly on it. Stay with this area.",
    };

    // --- ON TRACK: near target area, but not yet very close ---
    static readonly string[] k_GA_Hot_Track =
    {
        "You're on the right track.",
        "You're getting closer. Keep searching this direction.",
        "Good progress. You're moving toward it.",
        "This direction looks better. Keep going.",
        "You're in a better area now. Keep scanning.",
        "Nice adjustment. You're getting warmer.",
        "You're close. Keep working this side.",
        "Much better. Stay focused and keep scanning.",
        "You're narrowing it down. Keep at it.",
        "Good path. Keep looking around here.",
    };

    // --- FAR: wrong area ---
    static readonly string[] k_GA_Cold =
    {
        "You're way off right now.",
        "Look in a different area.",
        "This area isn't working. Shift your search.",
        "Not here. Try a different area.",
        "You're off target. Move your search.",
        "Let's switch areas and try again.",
        "Wrong area right now. Reposition your search.",
        "Don't stay here. Check another area.",
        "This isn't the zone. Move to a different area.",
        "Way off. Search somewhere else.",
    };

    // --- LESS WARM THAN LAST HINT ---
    static readonly string[] k_GA_Colder_Track =
    {
        "You're getting colder. You're still on the right track, keep scanning.",
        "You're getting colder. You're close, but adjust a little.",
        "You're getting colder. Keep searching this direction with small adjustments.",
        "You're getting colder now. You're near it, just refine your search.",
    };

    static readonly string[] k_GA_Colder_Cold =
    {
        "You're getting colder. Look in a different area.",
        "You're getting colder now. Move your search elsewhere.",
        "You're getting colder. This area is falling off.",
        "You're getting colder. Shift to another area.",
    };

    // =================================================================
    //  GAZE-UNAWARE (CONTROL) — no gaze, no location, just encouragement
    // =================================================================

    // Generic encouragement only — no proximity, no warmth, no spatial info,
    // no fake "you're close". Pure placebo presence so the participant gets
    // the same number of voice prompts as the gaze-aware condition without
    // any actual help finding the target.
    static readonly string[] k_GU_General =
    {
        "You're doing okay. Keep scanning.",
        "Take your time. Check shape and color.",
        "No rush. Scan one object at a time.",
        "You're making progress. Keep going.",
        "It's okay to slow down and compare carefully.",
        "Stay steady. Move to the next object when ready.",
        "You're doing fine. Recheck uncertain matches.",
        "Keep a calm pace. Continue searching.",
        "Good effort. Keep your attention on each object.",
        "You're on track. Keep scanning methodically.",
    };

    static readonly string[] k_GU_WrongCapture =
    {
        "Not this one, and that's okay. Try again.",
        "That wasn't the target. Take another look.",
        "Wrong one. You're okay, keep going.",
        "Not quite. Try another when you're ready.",
        "That wasn't it. Keep scanning calmly.",
        "Not the target. You're still doing fine.",
        "Wrong pick. Reset and keep searching.",
        "Not this one. Take your time and continue.",
    };
}
