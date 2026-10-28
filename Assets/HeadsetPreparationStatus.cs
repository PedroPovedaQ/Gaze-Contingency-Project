using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Small live-status sidecar available before replay has a trial header.
/// Never starts a trial, changes its clock, or includes interview answers or names.
/// </summary>
public sealed class HeadsetPreparationStatus : MonoBehaviour
{
    [Serializable]
    sealed class Status
    {
        public int schema = 1;
        public string sessionId = Guid.NewGuid().ToString("N");
        public long revision;
        public string phase = "ready";
        public string message = "Voice setup";
        public bool progressKnown;
        public float progress01;
        public double updatedUtc;
        public bool wallControlAvailable, wallsVisible;
        public string commandId, commandResult;
        public EyeValidationStatus validation;
        public bool validationAvailable;
        public AssistanceRuntimeSnapshot assistance;
        public string eyeRecordingId, eyeCaptureStatus;
        public bool eyeGazeTimestampAvailable, eyePupilTimestampAvailable, eyeHeadPoseTimeMatched;
        public bool intakeAvailable;
        public string participantSessionId, participantCode, configuration, assignmentState, nextParticipantCode;
    }

    [Serializable]
    sealed class Command
    {
        public int schema;
        public string id, sessionId, action;
        public long observedRevision;
        public double observedUtc;
        public bool wallsVisible;
        public ParticipantSessionAssignment assignment;
        public EyeValidationRequest validation;
    }

    [Serializable]
    sealed class ControlAudit
    {
        public string utc, sessionId, commandId, source = "trial_viewer", setting = "walls_and_degrees";
        public bool before, after;
        public int round;
        public string phase;
    }

    readonly Status m_Status = new Status();
    FindObjectGameManager m_Game;
    string m_Path;
    double m_NextWrite;
    long m_MinCommandRevision;
    bool m_Paused, m_Unfocused, m_Warned;

    void Awake()
    {
        m_Path = Path.Combine(Application.persistentDataPath, "PreparationLogs", "live-headset-state.json");
        m_Game = GetComponent<FindObjectGameManager>();
        if (m_Game != null)
        {
            m_Game.OnRoundTransitionStarted += Transition;
            m_Game.OnSearchStarted += SearchStarted;
            m_Game.OnSessionStopped += Stopped;
            m_Game.OnGameCompleted += Completed;
        }
        Write();
    }

    public void Set(string phase, string message, bool progressKnown = false, float progress = 0)
    {
        progress = float.IsNaN(progress) || float.IsInfinity(progress) ? 0 : Mathf.Clamp01(progress);
        bool changed = m_Status.phase != phase || m_Status.message != message ||
            m_Status.progressKnown != progressKnown;
        m_Status.phase = phase;
        m_Status.message = message;
        m_Status.progressKnown = progressKnown;
        m_Status.progress01 = progress;
        // Progress callbacks can arrive every frame; phase changes still write promptly
        // and ordinary progress is coalesced into the half-second heartbeat.
        if (changed) m_NextWrite = Math.Min(m_NextWrite, Time.realtimeSinceStartupAsDouble + .1);
    }

    void Update()
    {
        if (Time.realtimeSinceStartupAsDouble >= m_NextWrite)
        {
            ReadCommand();
            Write();
        }
    }

    static double UtcSeconds => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    void ReadCommand()
    {
        string path = Path.Combine(Path.GetDirectoryName(m_Path), "live-headset-command.json");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return;
            var command = JsonUtility.FromJson<Command>(File.ReadAllText(path));
            if (command == null || command.schema != 1 || !Guid.TryParseExact(command.id, "N", out _) ||
                command.sessionId != m_Status.sessionId || command.id == m_Status.commandId) return;
            // Use the headset heartbeat revision, never a comparison of Mac/headset clocks.
            m_Status.commandId = command.id;
            m_Status.commandResult = "rejected";
            if (m_Game == null || m_Paused || m_Unfocused ||
                command.observedRevision <= 0 || command.observedRevision < m_MinCommandRevision || command.observedRevision > m_Status.revision ||
                m_Status.revision - command.observedRevision > 20 ||
                double.IsNaN(command.observedUtc) || double.IsInfinity(command.observedUtc) ||
                UtcSeconds - command.observedUtc < 0 || UtcSeconds - command.observedUtc > 10) return;
            if (command.action != null && command.action.StartsWith("validation_", StringComparison.Ordinal))
            {
                if (EyeValidationSession.Instance?.Command(command.action, command.validation)==true) m_Status.commandResult="applied";
                return;
            }
            if (EyeValidationSession.Instance?.Active==true) return;
            if (command.action == "assign_session")
            {
                var selector = GetComponent<VoiceModeSelector>();
                if (command.assignment != null && selector != null && selector.StageDesktopSession(command.assignment))
                    m_Status.commandResult = "applied";
                return;
            }
            if (command.action != "set_walls") return;
            var audit = new ControlAudit { utc = DateTime.UtcNow.ToString("O"), sessionId = m_Status.sessionId,
                commandId = command.id, before = m_Game.DegreeGuidesVisible, after = command.wallsVisible,
                round = m_Game.CurrentObjectiveIndex, phase = m_Status.phase };
            string auditPath = Path.Combine(Path.GetDirectoryName(m_Path), "controls_" + m_Status.sessionId, "commands.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(auditPath));
            // Record the requested change before applying it; a failed audit leaves the setting unchanged.
            string detail = JsonUtility.ToJson(audit);
            File.AppendAllText(auditPath, detail + "\n");
            m_Game.SetDegreeGuidesVisible(command.wallsVisible);
            GetComponent<TrialReplayRecorder>()?.RecordHeadsetControl(detail);
            m_Status.commandResult = "applied";
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
        {
            // Keep the previous acknowledgment if no new valid command ID was parsed.
            // Identified requests are already marked rejected before any I/O or mutation.
        }
    }

    void Transition(int round, string color, string shape) => Set("ready", "Waiting for the next round");
    void SearchStarted(int round) => Set("trial", "Trial in progress");
    void Stopped(string reason) => Set("error", "Session stopped");
    void Completed(float elapsed) => Set("ready", "Session complete");

    void OnApplicationPause(bool paused) { m_Paused = paused; m_MinCommandRevision = m_Status.revision + 1; Write(); }
    void OnApplicationFocus(bool focused) { m_Unfocused = !focused; m_MinCommandRevision = m_Status.revision + 1; Write(); }
    void OnApplicationQuit() { m_Paused = true; Write(); }

    void Write()
    {
        if (string.IsNullOrEmpty(m_Path)) return;
        m_NextWrite = Time.realtimeSinceStartupAsDouble + .5;
        string phase = m_Status.phase, message = m_Status.message;
        bool known = m_Status.progressKnown;
        try
        {
            if (m_Paused || m_Unfocused)
            {
                m_Status.phase = "paused";
                m_Status.message = "Headset app paused";
                m_Status.progressKnown = false;
            }
            var selector = GetComponent<VoiceModeSelector>();
            m_Status.validation=EyeValidationSession.Instance?.Status;
            m_Status.validationAvailable=EyeValidationSession.Instance?.Available??false;
            m_Status.intakeAvailable = selector != null && selector.CanStageDesktopSession && !(m_Status.validation?.active??false);
            m_Status.participantSessionId = selector?.DesktopAssignmentId ?? "";
            m_Status.participantCode = selector?.DesktopAssignmentCode ?? "";
            m_Status.configuration = selector?.DesktopConfiguration ?? "";
            m_Status.assignmentState = selector?.DesktopAssignmentState ?? "none";
            if (string.IsNullOrEmpty(m_Status.nextParticipantCode) || m_Status.participantCode == m_Status.nextParticipantCode)
                m_Status.nextParticipantCode = SessionConfig.SuggestedParticipantId;
            m_Status.assistance = GetComponent<HintGenerator>()?.CaptureAssistanceStatus() ?? SessionConfig.CaptureAssistanceSelection();
            var eye=SharedGazeStream.Instance;
            var observation=eye?.Latest;
            double age=observation!=null ? Time.realtimeSinceStartupAsDouble-observation.htcQueryReceiptTime : double.PositiveInfinity;
            bool recent=observation!=null && observation.htcQueryReceiptTime>0 && age>=0 && age<.1;
            var timing=observation?.htcTiming;
            m_Status.eyeRecordingId=observation?.recordingId ?? "";
            m_Status.eyeGazeTimestampAvailable=recent && timing!=null && timing.gaze.observed==1 && timing.gaze.result==0 && timing.gaze.returnedTime>0;
            m_Status.eyePupilTimestampAvailable=recent && timing!=null && timing.pupil.observed==1 && timing.pupil.result==0 && timing.pupil.returnedTime>0;
            m_Status.eyeHeadPoseTimeMatched=recent && observation.htcHeadPoseValid;
            m_Status.eyeCaptureStatus=eye==null ? "Eye recording unavailable" : eye.RecordingError!=null ? "Eye recording error: "+eye.RecordingError :
                "Recording raw eye data; spatial accuracy, event detection and sensor cadence require validation";
            m_Status.wallControlAvailable = m_Game != null && !(m_Status.validation?.active??false);
            m_Status.wallsVisible = m_Game != null && m_Game.DegreeGuidesVisible;
            m_Status.revision++;
            m_Status.updatedUtc = UtcSeconds;
            Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
            string temp = m_Path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(m_Status));
            // Same-volume atomic rename prevents the receiver reading partial JSON.
            if (File.Exists(m_Path)) File.Replace(temp, m_Path, null);
            else File.Move(temp, m_Path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is NotSupportedException)
        {
            if (!m_Warned)
            {
                m_Warned = true;
                Debug.LogWarning("[HeadsetStatus] Live status unavailable: " + e.GetType().Name);
            }
        }
        finally
        {
            m_Status.phase = phase;
            m_Status.message = message;
            m_Status.progressKnown = known;
        }
    }

    void OnDestroy()
    {
        if (m_Game != null)
        {
            m_Game.OnRoundTransitionStarted -= Transition;
            m_Game.OnSearchStarted -= SearchStarted;
            m_Game.OnSessionStopped -= Stopped;
            m_Game.OnGameCompleted -= Completed;
        }
        m_Paused = true;
        Write();
    }
}
