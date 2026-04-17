using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

/// <summary>
/// Event-level trial logger that captures discrete game events and writes
/// a per-trial summary JSON at the end of each game.
///
/// Events CSV: timestamped capture/fixation/objective events.
/// Summary JSON: per-objective metrics, accuracy, timing, gaze behavior.
///
/// Auto-attaches to ObjectSpawner alongside FindObjectGameManager.
/// </summary>
public class TrialDataLogger : MonoBehaviour
{
    const string k_Tag = "[TrialLog]";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoAttach()
    {
        var spawner = FindObjectOfType<ObjectSpawner>();
        if (spawner != null && spawner.GetComponent<TrialDataLogger>() == null)
        {
            spawner.gameObject.AddComponent<TrialDataLogger>();
            Debug.Log($"{k_Tag} Auto-attached to {spawner.gameObject.name}");
        }
    }

    // --- Per-objective tracking ---
    struct ObjectiveRecord
    {
        public int index;
        public string shape;
        public string color;
        public float startTime;
        public float completionTime;
        public int wrongCaptures;
        public List<string> wrongCapturedObjects;
        public float fixationTimeOnTarget;
        public float fixationTimeOnDistractors;
        public int fixationCountOnTarget;
        public int fixationCountOnDistractors;
        // Saccade metrics — sum and count for averaging
        public int saccadeCount;
        public float totalSaccadeAmplitudeDeg;
        public bool completed;
    }

    FindObjectGameManager m_GameManager;
    GazeHighlightManager m_DwellSelector;
    GazeCoverageTracker m_CoverageTracker;
    GazeDataLogger m_GazeDataLogger;
    XRBaseInputInteractor m_GazeInteractor;

    StreamWriter m_EventWriter;
    string m_SessionId;
    string m_OutputDir;
    static readonly UTF8Encoding s_Utf8NoBom = new UTF8Encoding(false);

    readonly List<ObjectiveRecord> m_ObjectiveRecords = new();
    int m_ActiveObjectiveIndex = -1;
    string m_LastRunStatsText;

    // Fixation tracking (for per-objective fixation breakdown)
    string m_CurrentFixationObject;
    bool m_CurrentFixationIsTarget;
    float m_FixationStartTime;
    Vector3 m_CurrentFixationGazeDir;
    Vector3 m_LastFixationEndGazeDir;
    bool m_HasLastFixation;

    void OnEnable()
    {
        m_GameManager = GetComponent<FindObjectGameManager>();
        if (m_GameManager == null)
        {
            Debug.LogWarning($"{k_Tag} No FindObjectGameManager found, disabling");
            enabled = false;
            return;
        }

        m_GameManager.OnGameStarted += OnGameStarted;
        m_GameManager.OnObjectFound += OnObjectFound;
        m_GameManager.OnWrongCapture += OnWrongCapture;
        m_GameManager.OnGameCompleted += OnGameCompleted;

        Debug.Log($"{k_Tag} Initialized, waiting for game start");
    }

    void OnDisable()
    {
        if (m_GameManager != null)
        {
            m_GameManager.OnGameStarted -= OnGameStarted;
            m_GameManager.OnObjectFound -= OnObjectFound;
            m_GameManager.OnWrongCapture -= OnWrongCapture;
            m_GameManager.OnGameCompleted -= OnGameCompleted;
        }

        CloseEventWriter();
    }

    void Start()
    {
        // Resolve gaze interactor for fixation tracking
        var highlighter = FindObjectOfType<GazeHighlightManager>();
        if (highlighter != null)
        {
            m_DwellSelector = highlighter;
            m_GazeInteractor = highlighter.GetComponent<XRBaseInputInteractor>();
            m_GazeDataLogger = highlighter.GetComponent<GazeDataLogger>();
        }

        m_CoverageTracker = GetComponent<GazeCoverageTracker>();
    }

    // --- Game event handlers ---

    void OnGameStarted()
    {
        // Determine run-level condition label.
        // Current protocol alternates aware/unaware every round.
        string firstCondition = ChallengeSet.GetConditionLabel(0, 1);
        string secondCondition = ChallengeSet.GetConditionLabel(1, 1);
        string runConditionLabel = $"alternating_{firstCondition}_then_{secondCondition}";

        // Begin a new run — creates the output folder
        m_OutputDir = SessionConfig.BeginRun(runConditionLabel);
        m_SessionId = $"{SessionConfig.ParticipantId}_run{SessionConfig.RunNumber:D3}";

        // Open events CSV inside the run folder
        string eventsPath = SessionConfig.GetFilePath("trial_events.csv");
        m_EventWriter = new StreamWriter(eventsPath, false, s_Utf8NoBom);
        m_EventWriter.WriteLine(string.Join(",",
            "timestamp", "elapsed", "event_type",
            "objective_index", "objective_shape", "objective_color",
            "object_name", "object_shape", "object_color", "object_shelf_level",
            "is_target", "dwell_duration", "detail"
        ));

        // Initialize objective records
        m_ObjectiveRecords.Clear();
        var objectives = m_GameManager.Objectives;
        for (int i = 0; i < objectives.Count; i++)
        {
            m_ObjectiveRecords.Add(new ObjectiveRecord
            {
                index = i,
                shape = objectives[i].shape,
                color = objectives[i].color,
                startTime = i == 0 ? Time.time : 0f,
                wrongCaptures = 0,
                wrongCapturedObjects = new List<string>(),
                fixationTimeOnTarget = 0f,
                fixationTimeOnDistractors = 0f,
                fixationCountOnTarget = 0,
                fixationCountOnDistractors = 0,
                completed = false
            });
        }
        m_ActiveObjectiveIndex = 0;

        WriteEvent("game_start", "", "", "", -1, false, 0f,
            $"objectives={objectives.Count}");

        Debug.Log($"{k_Tag} Trial started: {eventsPath}");
    }

    void OnObjectFound(int objectiveIndex)
    {
        FinalizeCurrentFixation();

        if (objectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[objectiveIndex];
            rec.completionTime = Time.time;
            rec.completed = true;
            m_ObjectiveRecords[objectiveIndex] = rec;

            float timeToFind = rec.completionTime - rec.startTime;

            WriteEvent("capture_correct",
                rec.shape, rec.color, $"{rec.color}_{rec.shape}", -1,
                true, timeToFind,
                $"wrong_attempts={rec.wrongCaptures},time_to_find={timeToFind:F2}s");
        }

        // Reset saccade tracking between rounds (don't count cross-round saccades)
        m_HasLastFixation = false;

        // Start next objective
        int next = objectiveIndex + 1;
        if (next < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[next];
            rec.startTime = Time.time;
            m_ObjectiveRecords[next] = rec;
            m_ActiveObjectiveIndex = next;

            WriteEvent("objective_start",
                rec.shape, rec.color, "", -1, false, 0f,
                $"index={next}");
        }
    }

    void OnWrongCapture(string capturedName, string wantedName)
    {
        if (m_ActiveObjectiveIndex >= 0 && m_ActiveObjectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex];
            rec.wrongCaptures++;
            rec.wrongCapturedObjects.Add(capturedName);
            m_ObjectiveRecords[m_ActiveObjectiveIndex] = rec;
        }

        WriteEvent("capture_wrong",
            "", "", capturedName, -1, false, 0f,
            $"wanted={wantedName}");
    }

    void OnGameCompleted(float elapsedSeconds)
    {
        FinalizeCurrentFixation();

        WriteEvent("game_end", "", "", "", -1, false, elapsedSeconds,
            $"total_time={elapsedSeconds:F2}s,found={m_GameManager.FoundCount}");
        WriteEvent("nasa_tlx_prompt_shown", "", "", "", -1, false, 0f,
            "prompted_after_run_completion=1");

        // Write summary JSON
        WriteSummary(elapsedSeconds);
        m_LastRunStatsText = BuildParticipantStatsText(elapsedSeconds);

        CloseEventWriter();
        m_ActiveObjectiveIndex = -1;
    }

    // --- Per-frame fixation tracking ---

    void Update()
    {
        if (m_GazeInteractor == null || m_GameManager == null) return;
        if (m_GameManager.CurrentState != FindObjectGameManager.GameState.Playing) return;
        if (m_ActiveObjectiveIndex < 0 || m_ActiveObjectiveIndex >= m_ObjectiveRecords.Count) return;

        // Determine what's hovered
        string hoveredId = null;
        bool hoveredIsTarget = false;
        int hoveredLevel = -1;

        var hovered = m_GazeInteractor.interactablesHovered;
        if (hovered.Count > 0 && hovered[0] != null)
        {
            var info = hovered[0].transform.GetComponent<SpawnableObjectInfo>();
            if (info != null)
            {
                hoveredId = info.DisplayName;
                hoveredLevel = info.shelfLevel;

                var objectives = m_GameManager.Objectives;
                int idx = m_GameManager.CurrentObjectiveIndex;
                if (idx < objectives.Count)
                {
                    hoveredIsTarget = info.shapeName == objectives[idx].shape &&
                                     info.colorName == objectives[idx].color;
                }
            }
        }

        // Did fixation target change?
        if (hoveredId != m_CurrentFixationObject)
        {
            FinalizeCurrentFixation();

            if (hoveredId != null)
            {
                m_CurrentFixationObject = hoveredId;
                m_CurrentFixationIsTarget = hoveredIsTarget;
                m_FixationStartTime = Time.time;
                m_CurrentFixationGazeDir = m_GazeInteractor.transform.forward;

                // Saccade: angular distance from previous fixation to this one
                if (m_HasLastFixation && m_ActiveObjectiveIndex >= 0
                    && m_ActiveObjectiveIndex < m_ObjectiveRecords.Count)
                {
                    float angle = Vector3.Angle(m_LastFixationEndGazeDir, m_CurrentFixationGazeDir);
                    var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex];
                    rec.saccadeCount++;
                    rec.totalSaccadeAmplitudeDeg += angle;
                    m_ObjectiveRecords[m_ActiveObjectiveIndex] = rec;
                }

                WriteEvent("fixation_start",
                    "", "", hoveredId, hoveredLevel, hoveredIsTarget, 0f, "");
            }
            else
            {
                m_CurrentFixationObject = null;
            }
        }

        // Accumulate fixation time for current objective
        if (m_CurrentFixationObject != null)
        {
            var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex];
            if (m_CurrentFixationIsTarget)
                rec.fixationTimeOnTarget += Time.deltaTime;
            else
                rec.fixationTimeOnDistractors += Time.deltaTime;
            m_ObjectiveRecords[m_ActiveObjectiveIndex] = rec;
        }
    }

    void FinalizeCurrentFixation()
    {
        if (m_CurrentFixationObject == null || m_FixationStartTime <= 0f) return;

        float duration = Time.time - m_FixationStartTime;
        if (duration < 0.05f) // skip micro-glances
        {
            m_CurrentFixationObject = null;
            m_FixationStartTime = 0f;
            return;
        }

        // Update fixation count for current objective
        if (m_ActiveObjectiveIndex >= 0 && m_ActiveObjectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex];
            if (m_CurrentFixationIsTarget)
                rec.fixationCountOnTarget++;
            else
                rec.fixationCountOnDistractors++;
            m_ObjectiveRecords[m_ActiveObjectiveIndex] = rec;
        }

        // Save gaze dir for next saccade computation
        m_LastFixationEndGazeDir = m_GazeInteractor != null
            ? m_GazeInteractor.transform.forward : Vector3.forward;
        m_HasLastFixation = true;

        WriteEvent("fixation_end",
            "", "", m_CurrentFixationObject, -1, m_CurrentFixationIsTarget, duration,
            $"duration={duration:F3}s");

        m_CurrentFixationObject = null;
        m_FixationStartTime = 0f;
    }

    // --- CSV writing ---

    void WriteEvent(string eventType,
        string objShape, string objColor,
        string objectName, int shelfLevel,
        bool isTarget, float duration, string detail)
    {
        if (m_EventWriter == null) return;

        string currentObjShape = "", currentObjColor = "";
        int currentIdx = -1;
        if (m_ActiveObjectiveIndex >= 0 && m_ActiveObjectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex];
            currentObjShape = rec.shape;
            currentObjColor = rec.color;
            currentIdx = m_ActiveObjectiveIndex;
        }

        // Use event-specific shape/color if provided, else use current objective
        if (string.IsNullOrEmpty(objShape)) objShape = currentObjShape;
        if (string.IsNullOrEmpty(objColor)) objColor = currentObjColor;

        float elapsed = m_GameManager != null ? Time.time - m_GameManager.GameStartTime : 0f;

        m_EventWriter.WriteLine(string.Format("{0:F4},{1:F4},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F4},{12}",
            Time.time, elapsed, eventType,
            currentIdx, currentObjShape, currentObjColor,
            Sanitize(objectName), Sanitize(objShape), Sanitize(objColor), shelfLevel,
            isTarget ? 1 : 0, duration, Sanitize(detail)
        ));
        m_EventWriter.Flush();
    }

    // --- Summary JSON ---

    void WriteSummary(float totalTime)
    {
        string summaryPath = SessionConfig.GetFilePath("trial_summary.json");

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participant_id\": \"{SessionConfig.ParticipantId}\",");
        sb.AppendLine($"  \"run_number\": {SessionConfig.RunNumber},");
        sb.AppendLine($"  \"condition\": \"{SessionConfig.ConditionLabel}\",");
        sb.AppendLine($"  \"session_id\": \"{m_SessionId}\",");
        sb.AppendLine($"  \"timestamp\": \"{System.DateTime.Now:O}\",");
        sb.AppendLine($"  \"challenge_set\": \"deterministic_seed_42\",");
        sb.AppendLine($"  \"round_schedule\": \"alternating_gaze_unaware_gaze_aware\",");
        sb.AppendLine($"  \"total_rounds\": {ChallengeSet.RoundCount},");
        sb.AppendLine($"  \"nominal_total_rounds\": {ChallengeSet.TotalRounds},");
        sb.AppendLine($"  \"debug_round_override\": {ChallengeSet.DebugRoundCountOverride},");
        sb.AppendLine($"  \"rounds_per_block\": {ChallengeSet.RoundsPerBlock},");
        sb.AppendLine($"  \"objects_per_round\": {ChallengeSet.ObjectsPerRound},");
        sb.AppendLine($"  \"total_time_seconds\": {totalTime:F2},");
        sb.AppendLine($"  \"total_objectives\": {m_ObjectiveRecords.Count},");
        sb.AppendLine($"  \"objectives_completed\": {m_GameManager.FoundCount},");

        // Accuracy
        int correctFirstTry = 0;
        int totalWrong = 0;
        float totalFixationOnTarget = 0f;
        float totalFixationOnDistractors = 0f;

        foreach (var rec in m_ObjectiveRecords)
        {
            if (rec.completed && rec.wrongCaptures == 0) correctFirstTry++;
            totalWrong += rec.wrongCaptures;
            totalFixationOnTarget += rec.fixationTimeOnTarget;
            totalFixationOnDistractors += rec.fixationTimeOnDistractors;
        }

        float accuracy = m_GameManager.FoundCount > 0
            ? (float)correctFirstTry / m_GameManager.FoundCount : 0f;

        sb.AppendLine($"  \"correct_first_try\": {correctFirstTry},");
        sb.AppendLine($"  \"total_wrong_captures\": {totalWrong},");
        sb.AppendLine($"  \"first_try_accuracy\": {accuracy:F3},");
        sb.AppendLine($"  \"total_fixation_on_targets_seconds\": {totalFixationOnTarget:F2},");
        sb.AppendLine($"  \"total_fixation_on_distractors_seconds\": {totalFixationOnDistractors:F2},");

        // Blink stats
        bool hasBlinkSignal = m_GazeDataLogger != null && m_GazeDataLogger.HasBlinkSignal;
        int blinkCount = hasBlinkSignal ? m_GazeDataLogger.BlinkCount : -1;
        float blinksPerMinute = totalTime > 0 && blinkCount >= 0
            ? blinkCount / (totalTime / 60f) : -1f;
        sb.AppendLine($"  \"total_blinks\": {blinkCount},");
        sb.AppendLine($"  \"blinks_per_minute\": {(blinksPerMinute >= 0 ? blinksPerMinute.ToString("F1") : "\"-1\"")},");

        // Gaze behavior
        string behavior = m_CoverageTracker != null
            ? m_CoverageTracker.ClassifyBehavior().ToString()
            : "unknown";
        sb.AppendLine($"  \"gaze_behavior_classification\": \"{behavior}\",");

        // Per-objective breakdown
        sb.AppendLine("  \"objectives\": [");
        for (int i = 0; i < m_ObjectiveRecords.Count; i++)
        {
            var rec = m_ObjectiveRecords[i];
            float timeToFind = rec.completed ? rec.completionTime - rec.startTime : -1f;
            string wrongList = rec.wrongCapturedObjects != null && rec.wrongCapturedObjects.Count > 0
                ? "\"" + string.Join("\", \"", rec.wrongCapturedObjects) + "\""
                : "";

            int totalFixations = rec.fixationCountOnTarget + rec.fixationCountOnDistractors;
            float totalFixDuration = rec.fixationTimeOnTarget + rec.fixationTimeOnDistractors;
            float avgFixDuration = totalFixations > 0 ? totalFixDuration / totalFixations : 0f;
            float saccadeFreq = timeToFind > 0 ? rec.saccadeCount / timeToFind : 0f;
            float avgSaccadeAmp = rec.saccadeCount > 0 ? rec.totalSaccadeAmplitudeDeg / rec.saccadeCount : 0f;

            sb.AppendLine("    {");
            sb.AppendLine($"      \"index\": {rec.index},");
            sb.AppendLine($"      \"shape\": \"{rec.shape}\",");
            sb.AppendLine($"      \"color\": \"{rec.color}\",");
            sb.AppendLine($"      \"completed\": {(rec.completed ? "true" : "false")},");
            sb.AppendLine($"      \"time_to_find_seconds\": {timeToFind:F2},");
            sb.AppendLine($"      \"wrong_captures\": {rec.wrongCaptures},");
            sb.AppendLine($"      \"wrong_captured_objects\": [{wrongList}],");
            sb.AppendLine($"      \"fixation_time_on_target_seconds\": {rec.fixationTimeOnTarget:F2},");
            sb.AppendLine($"      \"fixation_time_on_distractors_seconds\": {rec.fixationTimeOnDistractors:F2},");
            sb.AppendLine($"      \"fixation_count_on_target\": {rec.fixationCountOnTarget},");
            sb.AppendLine($"      \"fixation_count_on_distractors\": {rec.fixationCountOnDistractors},");
            sb.AppendLine($"      \"fixation_count_total\": {totalFixations},");
            sb.AppendLine($"      \"avg_fixation_duration_seconds\": {avgFixDuration:F3},");
            sb.AppendLine($"      \"saccade_count\": {rec.saccadeCount},");
            sb.AppendLine($"      \"saccade_frequency_hz\": {saccadeFreq:F3},");
            sb.AppendLine($"      \"avg_saccade_amplitude_deg\": {avgSaccadeAmp:F2}");
            sb.Append("    }");
            if (i < m_ObjectiveRecords.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        File.WriteAllText(summaryPath, sb.ToString(), s_Utf8NoBom);
        Debug.Log($"{k_Tag} Trial summary written to {summaryPath}");
    }

    void CloseEventWriter()
    {
        if (m_EventWriter != null)
        {
            m_EventWriter.Flush();
            m_EventWriter.Close();
            m_EventWriter = null;
        }
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace(",", ";").Replace("\n", " ").Replace("\r", "");
    }

    public bool TryGetLastRunStatsText(out string text)
    {
        text = m_LastRunStatsText;
        return !string.IsNullOrEmpty(text);
    }

    public void RecordNasaTlx(int mental, int physical, int temporal,
        int performance, int effort, int frustration)
    {
        string root = SessionConfig.RootPath;
        if (!Directory.Exists(root))
            Directory.CreateDirectory(root);

        string path = Path.Combine(root, "nasa_tlx.csv");
        bool needsHeader = !File.Exists(path);

        using (var writer = new StreamWriter(path, true, s_Utf8NoBom))
        {
            if (needsHeader)
            {
                writer.WriteLine("participant_id,condition,mental,physical,temporal,performance,effort,frustration");
            }

            writer.WriteLine(string.Join(",",
                Sanitize(SessionConfig.ParticipantId),
                Sanitize(SessionConfig.ConditionLabel),
                mental.ToString(),
                physical.ToString(),
                temporal.ToString(),
                performance.ToString(),
                effort.ToString(),
                frustration.ToString()));
        }

        Debug.Log($"{k_Tag} NASA-TLX recorded: {path}");
    }

    string BuildParticipantStatsText(float totalTime)
    {
        int completed = 0;
        int correctFirstTry = 0;
        int totalWrong = 0;
        float totalFixTarget = 0f;
        float totalFixDistractor = 0f;
        float totalFindTime = 0f;

        for (int i = 0; i < m_ObjectiveRecords.Count; i++)
        {
            var rec = m_ObjectiveRecords[i];
            if (rec.completed)
            {
                completed++;
                totalFindTime += Mathf.Max(0f, rec.completionTime - rec.startTime);
                if (rec.wrongCaptures == 0) correctFirstTry++;
            }

            totalWrong += rec.wrongCaptures;
            totalFixTarget += rec.fixationTimeOnTarget;
            totalFixDistractor += rec.fixationTimeOnDistractors;
        }

        float firstTryPct = completed > 0 ? (100f * correctFirstTry / completed) : 0f;
        float avgFind = completed > 0 ? (totalFindTime / completed) : 0f;
        float fixTotal = totalFixTarget + totalFixDistractor;
        float targetFixPct = fixTotal > 0f ? (100f * totalFixTarget / fixTotal) : 0f;
        float distractorFixPct = fixTotal > 0f ? (100f * totalFixDistractor / fixTotal) : 0f;

        bool hasBlinkSignal = m_GazeDataLogger != null && m_GazeDataLogger.HasBlinkSignal;
        int blinkCount = hasBlinkSignal ? m_GazeDataLogger.BlinkCount : -1;
        float blinksPerMinute = totalTime > 0f && blinkCount >= 0
            ? blinkCount / (totalTime / 60f) : -1f;
        string behavior = m_CoverageTracker != null
            ? m_CoverageTracker.ClassifyBehavior().ToString()
            : "unknown";

        int minutes = (int)(totalTime / 60f);
        float seconds = totalTime % 60f;
        string timeStr = minutes > 0 ? $"{minutes}:{seconds:00.0}s" : $"{seconds:F1}s";

        var sb = new StringBuilder();
        sb.AppendLine("Session Stats");
        sb.AppendLine($"Rounds completed: {completed}/{ChallengeSet.RoundCount}");
        sb.AppendLine($"Total time: {timeStr}");
        sb.AppendLine($"First-try accuracy: {firstTryPct:F1}%");
        sb.AppendLine($"Wrong captures: {totalWrong}");
        sb.AppendLine($"Avg time to find target: {avgFind:F1}s");
        sb.AppendLine($"Fixation time on target: {targetFixPct:F1}%");
        sb.AppendLine($"Fixation time on distractors: {distractorFixPct:F1}%");
        if (blinkCount >= 0 && blinksPerMinute >= 0f)
            sb.AppendLine($"Blink rate: {blinksPerMinute:F1}/min");
        sb.AppendLine($"Gaze pattern: {behavior}");
        return sb.ToString().TrimEnd();
    }
}
