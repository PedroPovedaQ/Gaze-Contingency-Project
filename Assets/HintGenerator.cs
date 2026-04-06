using UnityEngine;

/// <summary>
/// Tip system — fires every 5 seconds. Two conditions:
///
/// GAZE-AWARE: Responds to the player's real-time gaze behavior.
/// Warmer/colder feedback, "you already checked there," "go back,
/// you just passed it," behavioral coaching. NEVER reveals the
/// target's location (shelf, direction, column).
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
    const float k_TipInterval = 5f;
    const float k_WrongCaptureWindow = 6f;

    /// <summary>
    /// When true, tips respond to gaze (warmer/colder, coaching, redundancy).
    /// When false, tips are generic encouragement only (control).
    /// Neither condition reveals the target's location.
    /// </summary>
    /// <summary>Always gaze-aware for now. Toggle for experiments later.</summary>
    public bool gazeAwareTips = true; // TODO: set via SessionConfig for experiment conditions

    AgentContext m_Context;
    VoiceSynthesizer m_Voice;
    GazeCoverageTracker m_CoverageTracker;
    FindObjectGameManager m_GameManager;

    float m_ObjectiveStartTime;
    float m_LastTipTime;
    float m_WrongCaptureTime;
    int m_LastPickIndex = -1;

    // Track what the player has already scanned (for "you already checked there")
    int m_LastGazedZone = -1;
    float m_TimeOnCurrentZone;
    bool m_WasGazingAtTarget;
    float m_LeftTargetTime;

    public void Initialize(string apiKey, AgentContext context, VoiceSynthesizer voice, GazeCoverageTracker coverageTracker = null)
    {
        m_Context = context;
        m_Voice = voice;
        m_CoverageTracker = coverageTracker;
    }

    void Start()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
    }

    public void OnNewObjective()
    {
        CancelPending();
        m_ObjectiveStartTime = Time.time;
        m_WrongCaptureTime = 0f;
        m_LastPickIndex = -1;
        m_WasGazingAtTarget = false;
        m_LeftTargetTime = 0f;
        m_LastGazedZone = -1;
        m_TimeOnCurrentZone = 0f;
    }

    public void OnWrongCapture()
    {
        m_WrongCaptureTime = Time.time;
    }

    public void CancelPending() { }

    void Update()
    {
        if (m_Voice == null || m_Context == null) return;
        if (m_GameManager == null || m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;

        // Track gaze-near-target state for "go back" detection
        bool gazingAtTarget = m_Context.IsGazingAtTarget();
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

        if (m_Voice.IsSpeaking) return;

        float now = Time.time;
        if (now - m_LastTipTime < k_TipInterval) return;

        string tip = gazeAwareTips ? EvaluateGazeAware(now) : EvaluateGazeUnaware(now);
        if (string.IsNullOrEmpty(tip)) return;

        m_LastTipTime = now;
        m_Voice.Speak(tip, "tip");
        Debug.Log($"{k_Tag} [{(gazeAwareTips ? "AWARE" : "UNAWARE")}] \"{tip}\"");
    }

    // =====================================================================
    //  GAZE-AWARE — responds to behavior, never reveals location
    // =====================================================================

    string EvaluateGazeAware(float now)
    {
        // --- Wrong capture ---
        if (m_WrongCaptureTime > 0f && (now - m_WrongCaptureTime) < k_WrongCaptureWindow)
        {
            m_WrongCaptureTime = 0f;
            return Pick(k_GA_WrongCapture);
        }

        // --- HOT: looking directly at the target ---
        if (m_Context.IsGazingAtTarget())
            return Pick(k_GA_Hot);

        // --- MISSED: just looked away from the target ---
        if (m_LeftTargetTime > 0f && (now - m_LeftTargetTime) < 5f)
        {
            m_LeftTargetTime = 0f;
            return Pick(k_GA_Missed);
        }

        // --- Temperature based on BOTH row and column proximity ---
        var (targetRow, targetCol) = GetTargetPosition();
        var (playerRow, playerCol) = GetPlayerPosition();

        // Can only give temperature when we know both positions
        if (targetRow >= 0 && playerRow >= 0 && targetCol >= 0 && playerCol >= 0)
        {
            int rowDist = Mathf.Abs(targetRow - playerRow);
            bool sameCol = targetCol == playerCol;

            if (sameCol && rowDist <= 1)
                return Pick(k_GA_Warm);     // right bookcase, close row
            else if (sameCol)
                return Pick(k_GA_Tepid);    // right bookcase, far row
            else if (rowDist == 0)
                return Pick(k_GA_Tepid);    // wrong bookcase, but right row height
            else if (rowDist <= 2)
                return Pick(k_GA_Tepid);    // wrong bookcase, close-ish row
            else
                return Pick(k_GA_Cold);     // wrong bookcase AND far row
        }

        // Not hovering any object — generic encouragement, NOT cold
        return Pick(k_GA_Tepid);
    }

    // =====================================================================
    //  GAZE-UNAWARE (control) — generic encouragement, no gaze reactivity
    // =====================================================================

    string EvaluateGazeUnaware(float now)
    {
        if (m_WrongCaptureTime > 0f && (now - m_WrongCaptureTime) < k_WrongCaptureWindow)
        {
            m_WrongCaptureTime = 0f;
            return Pick(k_GU_WrongCapture);
        }

        return Pick(k_GU_General);
    }

    // =====================================================================
    //  Helpers
    // =====================================================================

    (int row, int col) GetPlayerPosition()
    {
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter == null) return (-1, -1);
        var interactor = highlighter.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
        if (interactor == null) return (-1, -1);

        var hovered = interactor.interactablesHovered;
        if (hovered.Count > 0 && hovered[0] != null && hovered[0].transform != null)
        {
            var info = hovered[0].transform.GetComponent<SpawnableObjectInfo>();
            if (info != null) return (info.shelfLevel, info.shelfColumn);
        }
        return (-1, -1);
    }

    (int row, int col) GetTargetPosition()
    {
        if (m_GameManager == null) return (-1, -1);
        var objectives = m_GameManager.Objectives;
        int idx = m_GameManager.CurrentObjectiveIndex;
        if (idx >= objectives.Count) return (-1, -1);

        var target = objectives[idx];
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

    string Pick(string[] pool)
    {
        int idx;
        if (pool.Length <= 1) { idx = 0; }
        else { do { idx = Random.Range(0, pool.Length); } while (idx == m_LastPickIndex); }
        m_LastPickIndex = idx;
        return pool[idx];
    }

    // =================================================================
    //  GAZE-AWARE TIPS — hot/cold/missed temperature system
    // =================================================================

    // --- HOT: looking directly at the target ---
    static readonly string[] k_GA_Hot =
    {
        "Hot! Stay right there.",
        "That's it! Hold your gaze.",
        "You're on it! Don't look away.",
        "Hot hot hot! Keep looking.",
        "Yes! Stay focused right there.",
        "You've found it. Hold steady.",
        "That's the one! Keep your eyes on it.",
        "Burning hot! Don't move.",
        "Right on it! Almost captured.",
        "Lock in! That's it!",
    };

    // --- WARM: same shelf row as target but not on the exact object ---
    static readonly string[] k_GA_Warm =
    {
        "Warm! You're on the right row.",
        "Getting warmer. Keep scanning this row.",
        "Warm. It's right around here.",
        "You're close. Stay on this level.",
        "Getting hotter. Keep looking here.",
        "Warm! Check the other objects on this row.",
        "You're in the right area. Look more carefully.",
        "Almost. It's on this row somewhere.",
        "Warmer. Scan left and right on this level.",
        "Good area. Keep checking this row.",
    };

    // --- TEPID: one row away from target ---
    static readonly string[] k_GA_Tepid =
    {
        "Lukewarm. You're close but try a nearby row.",
        "Getting warmer. Try the row above or below.",
        "Tepid. Almost the right area.",
        "Not quite. But you're in the neighborhood.",
        "Close. Try shifting up or down a level.",
        "You're near it. Check the adjacent rows.",
        "Warm-ish. Just a row off.",
        "Almost the right zone. Shift up or down.",
        "You're one row away. Getting closer.",
        "Not bad. But look at the rows next to this one.",
    };

    // --- COLD: far from target (2+ rows away) ---
    static readonly string[] k_GA_Cold =
    {
        "Cold. Try a different area.",
        "Getting colder. Move away from here.",
        "Cold. It's not in this zone.",
        "Ice cold. Try somewhere else entirely.",
        "Not even close. Search a different section.",
        "Cold. You're far from it.",
        "Freezing. Try the other side.",
        "Way off. Keep searching other areas.",
        "Cold. Move on.",
        "Not here. Look somewhere completely different.",
    };

    // --- MISSED: player just looked away from the target ---
    static readonly string[] k_GA_Missed =
    {
        "Wait! Go back! You just passed it.",
        "You were just looking at it! Go back.",
        "Back up! You had it a moment ago.",
        "You were so close! Retrace your gaze.",
        "Go back to where you just were!",
        "You looked right past it! Go back.",
        "Stop! Go back!",
        "You missed it! It was right there!",
        "Turn back! You were on it!",
        "You just saw it! Go back!",
    };

    // --- Wrong capture ---
    static readonly string[] k_GA_WrongCapture =
    {
        "That wasn't it. Look more carefully.",
        "Wrong one. Check both shape and color.",
        "Not quite. Slow down.",
        "That's not it. Compare more carefully.",
        "Wrong pick. Take a closer look.",
        "Not it. Both shape and color must match.",
        "That wasn't right. Don't rush.",
        "Wrong one. Double check before locking in.",
        "Not the target. Look again.",
        "Missed it. Try again.",
    };

    // =================================================================
    //  GAZE-UNAWARE (CONTROL) — no gaze, no location, just encouragement
    // =================================================================

    static readonly string[] k_GU_General =
    {
        "Keep looking. You'll find it.",
        "Take your time.",
        "Stay focused.",
        "You've got this.",
        "Keep searching.",
        "Don't give up.",
        "Be patient.",
        "Stay sharp.",
        "You're doing fine. Keep going.",
        "Concentrate.",
        "Keep at it.",
        "Stay calm and search.",
        "You'll get it.",
        "No rush.",
        "Keep your focus.",
        "Almost there.",
        "Hang in there.",
        "Steady.",
        "Keep going.",
        "You can do this.",
    };

    static readonly string[] k_GU_WrongCapture =
    {
        "Not quite. Try again.",
        "That wasn't it. Keep trying.",
        "Wrong one. You'll get it.",
        "Not this one. Try again.",
        "Almost. Keep looking.",
        "That's not it. No worries.",
        "Wrong pick. Keep going.",
        "Not quite right. Try another.",
        "That wasn't the one.",
        "Not it. Keep going.",
    };
}
