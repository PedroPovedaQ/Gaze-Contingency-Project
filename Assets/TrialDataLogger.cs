using System;
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
        public float transitionTime;
        public float objectsReadyTime;
        public string voiceCondition;
        public string outcome;
        public float completionTime;
        public float searchSeconds;
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
    VoiceSynthesizer m_Voice;
    string m_SessionOutcome = "incomplete";
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
        m_GameManager.OnSearchStarted += OnSearchStarted;
        m_GameManager.OnRoundTransitionStarted += OnTransitionStarted;
        m_GameManager.OnRoundReady += OnObjectsReady;
        m_GameManager.OnSessionStopped += OnSessionStopped;
        m_GameManager.OnCheckpoint += OnCheckpoint;

        Debug.Log($"{k_Tag} Initialized, waiting for game start");
    }

    void OnDisable()
    {
        if (m_EventWriter != null) OnSessionStopped("interrupted");
        if (m_GameManager != null)
        {
            m_GameManager.OnGameStarted -= OnGameStarted;
            m_GameManager.OnObjectFound -= OnObjectFound;
            m_GameManager.OnWrongCapture -= OnWrongCapture;
            m_GameManager.OnGameCompleted -= OnGameCompleted;
            m_GameManager.OnSearchStarted -= OnSearchStarted;
            m_GameManager.OnRoundTransitionStarted -= OnTransitionStarted;
            m_GameManager.OnRoundReady -= OnObjectsReady;
            m_GameManager.OnSessionStopped -= OnSessionStopped;
            m_GameManager.OnCheckpoint -= OnCheckpoint;
        }

        if (m_Voice != null) m_Voice.Telemetry -= OnAudioEvent;
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
        // Begin a new run — creates the output folder
        m_OutputDir = SessionConfig.BeginRun();
        string manifest = Path.Combine(SessionConfig.ParticipantPath, "voice-library-manifest.json");
        if (File.Exists(manifest)) File.Copy(manifest, SessionConfig.GetFilePath("voice-library-manifest.json"), true);
        m_Voice = GetComponent<VoiceSynthesizer>();
        if (m_Voice != null) m_Voice.Telemetry += OnAudioEvent;
        m_SessionOutcome = "incomplete";
        m_SessionId = $"{SessionConfig.ParticipantId}_run{SessionConfig.RunNumber:D3}";

        // Open events CSV inside the run folder
        string eventsPath = SessionConfig.GetFilePath("trial_events.csv");
        m_EventWriter = new StreamWriter(eventsPath, false, s_Utf8NoBom);
        m_EventWriter.WriteLine(string.Join(",",
            "timestamp", "elapsed", "event_type",
            "objective_index", "objective_shape", "objective_color",
            "object_name", "object_shape", "object_color", "object_shelf_level",
            "is_target", "dwell_duration", "detail", "participant_id", "run_number", "block", "voice_condition", "trial_id"
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
                startTime = -1f,
                transitionTime = -1f, objectsReadyTime = -1f,
                voiceCondition = SessionConfig.VoiceBlocksEnabled ? SessionConfig.VoiceLabelForRound(i) : SessionConfig.VoiceTag,
                outcome = "not_started",
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

    void OnTransitionStarted(int round, string color, string shape)
    {
        if (round < 0 || round >= m_ObjectiveRecords.Count) return;
        m_ActiveObjectiveIndex = round;
        var rec = m_ObjectiveRecords[round]; rec.transitionTime = Time.time; rec.outcome = "transitioning";
        m_ObjectiveRecords[round] = rec;
        WriteEvent("transition_start", "", "", "", -1, false, 0, "");
    }
    void OnObjectsReady(int round, string color, string shape)
    {
        if (m_GameManager.IsPractice || round >= m_ObjectiveRecords.Count) return;
        var rec = m_ObjectiveRecords[round]; rec.objectsReadyTime = Time.time; m_ObjectiveRecords[round] = rec;
        WriteEvent("objects_ready", "", "", "", -1, false, 0, "");
        WriteObjectManifest(round);
    }
    void OnSearchStarted(int round)
    {
        if (m_GameManager.IsPractice || round >= m_ObjectiveRecords.Count) return;
        m_ActiveObjectiveIndex = round;
        var rec = m_ObjectiveRecords[round]; rec.startTime = Time.time; rec.outcome = "searching";
        m_ObjectiveRecords[round] = rec; m_HasLastFixation = false;
        WriteEvent("search_start", "", "", "", -1, false, 0, "");
    }
    void OnCheckpoint(string name)
    {
        if (m_EventWriter == null) return;
        if (name == "block_start") m_ActiveObjectiveIndex = m_GameManager.CurrentObjectiveIndex;
        if (name == "search_paused") FinalizeCurrentFixation();
        WriteEvent(name, "", "", "", -1, false, 0, "researcher_checkpoint");
        WriteSummary(Time.time - m_GameManager.GameStartTime);
    }
    void OnAudioEvent(string kind, string context, string clip, string detail)
    {
        WriteEvent(kind, "", "", "", -1, false, 0, $"context={context};clip_id={clip};{detail}");
    }
    void OnSessionStopped(string reason)
    {
        if (m_EventWriter == null) return;
        FinalizeCurrentFixation();
        m_SessionOutcome = reason.StartsWith("audio_failure") ? "technical_failure" : reason;
        if (m_ActiveObjectiveIndex >= 0 && m_ActiveObjectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[m_ActiveObjectiveIndex]; rec.outcome = m_SessionOutcome;
            rec.searchSeconds = rec.startTime >= 0 ? m_GameManager.CurrentSearchSeconds : 0;
            m_ObjectiveRecords[m_ActiveObjectiveIndex] = rec;
        }
        WriteEvent("session_stopped", "", "", "", -1, false, 0, reason);
        WriteSummary(Time.time - m_GameManager.GameStartTime);
        CloseEventWriter();
    }
    void WriteObjectManifest(int round)
    {
        string path = SessionConfig.GetFilePath("object_manifest.csv");
        bool header = !File.Exists(path);
        using (var writer = new StreamWriter(path, true, s_Utf8NoBom))
        {
            if (header) writer.WriteLine("trial_id,object_id,shape,color,row,column,x,y,z,is_target");
            foreach (var obj in m_GameManager.SpawnedObjects)
            {
                var info = obj.GetComponent<SpawnableObjectInfo>(); if (info == null) continue;
                var pos = obj.transform.position;
                bool target = info.shapeName == m_ObjectiveRecords[round].shape && info.colorName == m_ObjectiveRecords[round].color;
                writer.WriteLine(FormattableString.Invariant($"{m_SessionId}_r{round:D2},{info.objectId},{info.shapeName},{info.colorName},{info.shelfLevel},{info.shelfColumn},{pos.x:F5},{pos.y:F5},{pos.z:F5},{(target ? 1 : 0)}"));
            }
        }
    }

    void OnObjectFound(int objectiveIndex)
    {
        FinalizeCurrentFixation();

        if (objectiveIndex < m_ObjectiveRecords.Count)
        {
            var rec = m_ObjectiveRecords[objectiveIndex];
            rec.completionTime = Time.time;
            rec.searchSeconds = m_GameManager.CurrentSearchSeconds;
            rec.completed = true; rec.outcome = "completed";
            m_ObjectiveRecords[objectiveIndex] = rec;

            float timeToFind = rec.searchSeconds;

            WriteEvent("capture_correct",
                rec.shape, rec.color, $"{rec.color}_{rec.shape}", -1,
                true, timeToFind,
                $"wrong_attempts={rec.wrongCaptures},time_to_find={timeToFind:F2}s");
        }

        // Reset saccade tracking between rounds (don't count cross-round saccades)
        m_HasLastFixation = false;

        // Retain the completed trial identity until the next transition starts.
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
        m_SessionOutcome = "completed";

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
        if (!m_GameManager.SearchActive) return;
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
                hoveredId = info.objectId;
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

        m_EventWriter.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11:F4},{12},{13},{14},{15},{16},{17}",
            Time.time, elapsed, eventType,
            currentIdx, currentObjShape, currentObjColor,
            Sanitize(objectName), Sanitize(objShape), Sanitize(objColor), shelfLevel,
            isTarget ? 1 : 0, duration, Sanitize(detail),
            SessionConfig.ParticipantId, SessionConfig.RunNumber, currentIdx < 0 ? -1 : currentIdx / ChallengeSet.RoundsPerBlock,
            currentIdx < 0 ? SessionConfig.VoiceTag : m_ObjectiveRecords[currentIdx].voiceCondition,
            currentIdx < 0 ? "" : $"{m_SessionId}_r{currentIdx:D2}"
        ));
        m_EventWriter.Flush();
    }

    // --- Summary JSON ---

    void WriteSummary(float totalTime)
    {
        float wallTime = Time.time - m_GameManager.GameStartTime;
        totalTime = 0;
        for (int i = 0; i < m_ObjectiveRecords.Count; i++)
        {
            var record = m_ObjectiveRecords[i];
            if (i == m_ActiveObjectiveIndex && record.startTime >= 0 && !record.completed)
            { record.searchSeconds = m_GameManager.CurrentSearchSeconds; m_ObjectiveRecords[i] = record; }
            totalTime += record.searchSeconds;
        }
        string summaryPath = SessionConfig.GetFilePath("trial_summary.json");

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine(FormattableString.Invariant($"  \"participant_id\": \"{SessionConfig.ParticipantId}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"run_number\": {SessionConfig.RunNumber},"));
        sb.AppendLine(FormattableString.Invariant($"  \"condition\": \"{SessionConfig.ConditionLabel}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"voice_condition\": \"{(SessionConfig.VoiceBlocksEnabled ? "counterbalanced" : SessionConfig.VoiceTag)}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"neutral_voice_profile\": \"{SessionConfig.NeutralProfile.ToString().ToLowerInvariant()}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"session_id\": \"{m_SessionId}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"timestamp\": \"{System.DateTime.Now:O}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"challenge_set\": \"deterministic_seed_42\","));
        sb.AppendLine(FormattableString.Invariant($"  \"round_schedule\": \"always_gaze_aware\","));
        sb.AppendLine(FormattableString.Invariant($"  \"schema_version\": 2,"));
        sb.AppendLine(FormattableString.Invariant($"  \"session_outcome\": \"{m_SessionOutcome}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"voice_order\": \"{SessionConfig.VoiceOrder}\","));
        sb.AppendLine(FormattableString.Invariant($"  \"total_rounds\": {ChallengeSet.RoundCount},"));
        sb.AppendLine(FormattableString.Invariant($"  \"nominal_total_rounds\": {ChallengeSet.TotalRounds},"));
        sb.AppendLine(FormattableString.Invariant($"  \"debug_round_override\": {ChallengeSet.DebugRoundCountOverride},"));
        sb.AppendLine(FormattableString.Invariant($"  \"rounds_per_block\": {ChallengeSet.RoundsPerBlock},"));
        sb.AppendLine(FormattableString.Invariant($"  \"objects_per_round\": {ChallengeSet.ObjectsPerRound},"));
        sb.AppendLine(FormattableString.Invariant($"  \"total_time_seconds\": {totalTime:F2},"));
        sb.AppendLine(FormattableString.Invariant($"  \"session_wall_time_seconds\": {wallTime:F4},"));
        sb.AppendLine(FormattableString.Invariant($"  \"total_objectives\": {m_ObjectiveRecords.Count},"));
        sb.AppendLine(FormattableString.Invariant($"  \"objectives_completed\": {m_GameManager.FoundCount},"));

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

        sb.AppendLine(FormattableString.Invariant($"  \"correct_first_try\": {correctFirstTry},"));
        sb.AppendLine(FormattableString.Invariant($"  \"total_wrong_captures\": {totalWrong},"));
        sb.AppendLine(FormattableString.Invariant($"  \"first_try_accuracy\": {accuracy:F3},"));
        sb.AppendLine(FormattableString.Invariant($"  \"total_fixation_on_targets_seconds\": {totalFixationOnTarget:F2},"));
        sb.AppendLine(FormattableString.Invariant($"  \"total_fixation_on_distractors_seconds\": {totalFixationOnDistractors:F2},"));

        // Blink stats
        bool hasBlinkSignal = m_GazeDataLogger != null && m_GazeDataLogger.HasBlinkSignal;
        int blinkCount = hasBlinkSignal ? m_GazeDataLogger.BlinkCount : -1;
        float blinksPerMinute = wallTime > 0 && blinkCount >= 0
            ? blinkCount / (wallTime / 60f) : -1f;
        sb.AppendLine(FormattableString.Invariant($"  \"total_blinks\": {blinkCount},"));
        sb.AppendLine(FormattableString.Invariant($"  \"blinks_per_minute\": {blinksPerMinute:F1},"));

        // Gaze behavior
        string behavior = m_CoverageTracker != null
            ? m_CoverageTracker.ClassifyBehavior().ToString()
            : "unknown";
        sb.AppendLine(FormattableString.Invariant($"  \"gaze_behavior_classification\": \"{behavior}\","));

        // Per-objective breakdown
        sb.AppendLine("  \"objectives\": [");
        for (int i = 0; i < m_ObjectiveRecords.Count; i++)
        {
            var rec = m_ObjectiveRecords[i];
            float timeToFind = rec.completed ? rec.searchSeconds : -1f;
            string wrongList = rec.wrongCapturedObjects != null && rec.wrongCapturedObjects.Count > 0
                ? "\"" + string.Join("\", \"", rec.wrongCapturedObjects) + "\""
                : "";

            int totalFixations = rec.fixationCountOnTarget + rec.fixationCountOnDistractors;
            float totalFixDuration = rec.fixationTimeOnTarget + rec.fixationTimeOnDistractors;
            float avgFixDuration = totalFixations > 0 ? totalFixDuration / totalFixations : 0f;
            float saccadeFreq = timeToFind > 0 ? rec.saccadeCount / timeToFind : 0f;
            float avgSaccadeAmp = rec.saccadeCount > 0 ? rec.totalSaccadeAmplitudeDeg / rec.saccadeCount : 0f;

            sb.AppendLine("    {");
            sb.AppendLine(FormattableString.Invariant($"      \"index\": {rec.index},"));
            sb.AppendLine(FormattableString.Invariant($"      \"shape\": \"{rec.shape}\","));
            sb.AppendLine(FormattableString.Invariant($"      \"color\": \"{rec.color}\","));
            sb.AppendLine(FormattableString.Invariant($"      \"completed\": {(rec.completed ? "true" : "false")},"));
            sb.AppendLine(FormattableString.Invariant($"      \"time_to_find_seconds\": {timeToFind:F2},"));
            sb.AppendLine(FormattableString.Invariant($"      \"search_exposure_seconds\": {rec.searchSeconds:F4},"));
            sb.AppendLine(FormattableString.Invariant($"      \"voice_condition\": \"{rec.voiceCondition}\","));
            sb.AppendLine(FormattableString.Invariant($"      \"block\": {rec.index / ChallengeSet.RoundsPerBlock},"));
            sb.AppendLine(FormattableString.Invariant($"      \"trial_id\": \"{m_SessionId}_r{rec.index:D2}\","));
            sb.AppendLine(FormattableString.Invariant($"      \"outcome\": \"{rec.outcome}\","));
            sb.AppendLine(FormattableString.Invariant($"      \"transition_started_at\": {rec.transitionTime:F4},"));
            sb.AppendLine(FormattableString.Invariant($"      \"objects_ready_at\": {rec.objectsReadyTime:F4},"));
            sb.AppendLine(FormattableString.Invariant($"      \"search_started_at\": {rec.startTime:F4},"));
            sb.AppendLine(FormattableString.Invariant($"      \"capture_at\": {(rec.completed ? rec.completionTime : -1f):F4},"));
            sb.AppendLine(FormattableString.Invariant($"      \"wrong_captures\": {rec.wrongCaptures},"));
            sb.AppendLine(FormattableString.Invariant($"      \"wrong_captured_objects\": [{wrongList}],"));
            sb.AppendLine(FormattableString.Invariant($"      \"fixation_time_on_target_seconds\": {rec.fixationTimeOnTarget:F2},"));
            sb.AppendLine(FormattableString.Invariant($"      \"fixation_time_on_distractors_seconds\": {rec.fixationTimeOnDistractors:F2},"));
            sb.AppendLine(FormattableString.Invariant($"      \"fixation_count_on_target\": {rec.fixationCountOnTarget},"));
            sb.AppendLine(FormattableString.Invariant($"      \"fixation_count_on_distractors\": {rec.fixationCountOnDistractors},"));
            sb.AppendLine(FormattableString.Invariant($"      \"fixation_count_total\": {totalFixations},"));
            sb.AppendLine(FormattableString.Invariant($"      \"avg_fixation_duration_seconds\": {avgFixDuration:F3},"));
            sb.AppendLine(FormattableString.Invariant($"      \"saccade_count\": {rec.saccadeCount},"));
            sb.AppendLine(FormattableString.Invariant($"      \"saccade_frequency_hz\": {saccadeFreq:F3},"));
            sb.AppendLine(FormattableString.Invariant($"      \"avg_saccade_amplitude_deg\": {avgSaccadeAmp:F2}"));
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
        int performance, int effort, int frustration, int block = -1)
    {
        string root = SessionConfig.RootPath;
        if (!Directory.Exists(root))
            Directory.CreateDirectory(root);

        string path = block >= 0 ? SessionConfig.GetFilePath("nasa_tlx.csv") : Path.Combine(root, "nasa_tlx.csv");
        bool needsHeader = !File.Exists(path);

        using (var writer = new StreamWriter(path, true, s_Utf8NoBom))
        {
            if (needsHeader)
            {
                writer.WriteLine("participant_id,condition,mental,physical,temporal,performance,effort,frustration" + (block >= 0 ? ",run_number,block" : ""));
            }

            writer.WriteLine(string.Join(",",
                Sanitize(SessionConfig.ParticipantId),
                Sanitize(block >= 0 ? "gaze_aware_voice-" + SessionConfig.VoiceTag : SessionConfig.ConditionLabel),
                mental.ToString(),
                physical.ToString(),
                temporal.ToString(),
                performance.ToString(),
                effort.ToString(),
                frustration.ToString()) + (block >= 0 ? $",{SessionConfig.RunNumber},{block}" : ""));
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
                totalFindTime += Mathf.Max(0f, rec.searchSeconds);
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
        sb.AppendLine(FormattableString.Invariant($"Rounds completed: {completed}/{ChallengeSet.RoundCount}"));
        sb.AppendLine(FormattableString.Invariant($"Total time: {timeStr}"));
        sb.AppendLine(FormattableString.Invariant($"First-try accuracy: {firstTryPct:F1}%"));
        sb.AppendLine(FormattableString.Invariant($"Wrong captures: {totalWrong}"));
        sb.AppendLine(FormattableString.Invariant($"Avg time to find target: {avgFind:F1}s"));
        sb.AppendLine(FormattableString.Invariant($"Fixation time on target: {targetFixPct:F1}%"));
        sb.AppendLine(FormattableString.Invariant($"Fixation time on distractors: {distractorFixPct:F1}%"));
        if (blinkCount >= 0 && blinksPerMinute >= 0f)
            sb.AppendLine(FormattableString.Invariant($"Blink rate: {blinksPerMinute:F1}/min"));
        sb.AppendLine(FormattableString.Invariant($"Gaze pattern: {behavior}"));
        return sb.ToString().TrimEnd();
    }
}
