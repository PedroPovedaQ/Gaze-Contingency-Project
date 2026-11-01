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
        public ParticipantFitMeasurements fit;
        public string participantSessionId, participantCode, configuration, assignmentState, nextParticipantCode;
        public string screen, voiceBlock, gazeStatus, audioFault, modelFault;
        public double screenElapsedSeconds, sessionElapsedSeconds;
        public int trialsCompleted, trialTotal = 96;
        public bool appPaused, appFocused, presenceKnown, userPresent;
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
        public ParticipantFitMeasurements fit;
        public string participantSessionId;
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
    string m_MonitorSession, m_LastScreen;
    double m_ScreenSince, m_SessionSince;

    void Awake()
    {
        m_Path = Path.Combine(Application.persistentDataPath, "PreparationLogs", "live-headset-state.json");
        m_Game = GetComponent<FindObjectGameManager>();
#if UNITY_5_3_OR_NEWER
        if (GetComponent<ParticipantFitCapture>() == null) gameObject.AddComponent<ParticipantFitCapture>();
#endif
        if (m_Game != null)
        {
            m_Game.OnRoundTransitionStarted += Transition;
            m_Game.OnSearchStarted += SearchStarted;
            m_Game.OnSessionStopped += Stopped;
            m_Game.OnGameCompleted += Completed;
#if UNITY_5_3_OR_NEWER
            m_Game.OnReadinessEvent += Readiness;
            m_Game.OnObjectFound += Selected;
            m_Game.OnRoundTimedOut += TimedOut;
            m_Game.OnCheckpoint += Checkpoint;
            m_Game.OnWrongCapture += WrongSelection;
#endif
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
        if (changed) Write(); // Stage boundaries are rare; do not lose short screens between heartbeats.
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

    // Setup screens can change between heartbeats; record their boundary without commands.
    public void RefreshScreen() => Write();

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
            if (command.action == "set_fit_fallback")
            {
                if (command.participantSessionId != SessionConfig.DesktopSessionId || string.IsNullOrEmpty(command.participantSessionId) || command.fit == null) return;
                command.fit.Validate();
                var fit = SessionConfig.ParticipantFit;
                if (command.fit.ipdRecorded && fit.eyeSeparation?.recorded == true || command.fit.headsetIpdRecorded && fit.viewSeparation?.recorded == true) return;
                if (command.fit.ipdRecorded)
                { fit.ipdRecorded = true; fit.participantIpdMm = command.fit.participantIpdMm; fit.ipdSource = command.fit.ipdSource; }
                if (command.fit.headsetIpdRecorded)
                { fit.headsetIpdRecorded = true; fit.headsetIpdMm = command.fit.headsetIpdMm; }
                fit.manualRecordedUtc = DateTime.UtcNow.ToString("O"); SessionConfig.SaveParticipantFit();
                m_Status.commandResult = "applied";
                return;
            }
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

    void Transition(int round, string color, string shape) => Set("ready", "Readiness cross");
    void SearchStarted(int round) => Set("trial", "Active search");
    void Stopped(string reason) => Set("error", "Session stopped");
    void Completed(float elapsed) => Set("ready", "Finished · survey handoff");
#if UNITY_5_3_OR_NEWER
    void Selected(int round) => Set("ready", "Selection feedback");
    void TimedOut(int round) => Set("ready", "Timeout feedback");
    void Checkpoint(string stage)
    {
        switch(stage)
        {
            case "block_end": Set("ready","Block break · comfort and tracking check");break;
            case "block_start": Set("ready","Block 2 · ready to begin");break;
            case "search_paused": Set("paused","Session paused · researcher checkpoint");break;
            case "readiness_resumed": Set("ready","Readiness cross");break;
            case "search_resumed": Set("trial","Active search");break;
        }
    }
    void WrongSelection(string selected, string target) => Write();
    void Readiness(string stage)
    {
        if(stage == "readiness_completed") Set("ready", "Opening cue");
        else if(stage == "readiness_started") Set("ready", "Readiness cross");
    }
    void UpdateMonitor(VoiceModeSelector selector, string currentMessage)
    {
        double now = Time.realtimeSinceStartupAsDouble;
        if(m_MonitorSession != m_Status.participantSessionId)
        { m_MonitorSession=m_Status.participantSessionId; m_SessionSince=now; m_LastScreen=null; }
        string screen = selector != null && !selector.IsComplete ? selector.MonitorScreen : currentMessage;
        var validation = m_Status.validation;
        if(validation?.active == true) screen = "Verification · " + validation.stage + " · dot " + (validation.step+1) + " of " + validation.count;
        bool practice = m_Game != null && m_Game.IsPractice;
        if(m_Game?.SearchActive==true && GetComponent<FindObjectUI>()?.WrongFeedbackVisible==true)
            screen="Wrong selection feedback · continue searching";
        if(m_Game != null && (screen=="Readiness cross" || screen=="Opening cue" || screen=="Active search" || screen=="Selection feedback" || screen=="Timeout feedback"))
            screen = (practice ? "Practice · " : "Trial " + (Math.Max(0,m_Game.PresentedRoundIndex)+1) + " of 96 · ") + screen;
        m_Status.appPaused=m_Paused; m_Status.appFocused=!m_Unfocused;
        var head=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
        m_Status.presenceKnown=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence,out m_Status.userPresent);
        if(m_Paused) screen="Headset app paused / sleeping · " + screen;
        else if(m_Unfocused) screen="Headset app out of focus · " + screen;
        else if(m_Status.presenceKnown && !m_Status.userPresent) screen="Headset removed · " + screen;
        m_Status.trialsCompleted=m_Game == null || practice ? 0 : Math.Min(96,Math.Max(0,m_Game.CurrentObjectiveIndex));
        int round=Math.Max(0,m_Game?.PresentedRoundIndex ?? 0);
        if(currentMessage=="Block 2 · ready to begin")round=48;
        bool selected=(round<48)==(m_Status.configuration=="B");
        m_Status.voiceBlock="Block " + (round/48+1) + " · " + (selected?"Selected Voice":"Self-Similar Voice");
        m_Status.gazeStatus=SharedGazeStream.Instance?.TryGetValid(out _)==true ? "Tracking" : "Tracking unavailable or stale";
        m_Status.audioFault=GetComponent<VoiceSynthesizer>()?.LastError ?? "";
        m_Status.modelFault=m_Status.assistance?.connection=="unavailable" || m_Status.assistance?.connection=="not_configured" ? "Guidance model " + m_Status.assistance.ConnectionLabel : "";
        if(screen != m_LastScreen)
        {
            m_LastScreen=screen; m_ScreenSince=now;
            if(!string.IsNullOrEmpty(m_MonitorSession))
            {
                string folder=Path.Combine(Path.GetDirectoryName(m_Path),"session-"+m_MonitorSession);
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder,"screen-transitions.jsonl"),JsonUtility.ToJson(new ScreenTransition {
                    utc=DateTime.UtcNow.ToString("O"),sessionId=m_MonitorSession,headsetSessionId=m_Status.sessionId,participantCode=m_Status.participantCode,
                    screen=screen,sessionElapsedSeconds=now-m_SessionSince,trialsCompleted=m_Status.trialsCompleted,voiceBlock=m_Status.voiceBlock })+"\n");
            }
        }
        m_Status.screen=screen; m_Status.screenElapsedSeconds=now-m_ScreenSince;
        m_Status.sessionElapsedSeconds=string.IsNullOrEmpty(m_MonitorSession)?0:now-m_SessionSince;
    }
    [Serializable] sealed class ScreenTransition
    {
        public string utc, sessionId, headsetSessionId, participantCode, screen, voiceBlock;
        public double sessionElapsedSeconds;
        public int trialsCompleted;
    }
#endif

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
            m_Status.fit = SessionConfig.DesktopSessionId == m_Status.participantSessionId ? SessionConfig.ParticipantFit.Copy() : null;
            if (string.IsNullOrEmpty(m_Status.nextParticipantCode) || m_Status.participantCode == m_Status.nextParticipantCode)
                m_Status.nextParticipantCode = SessionConfig.SuggestedParticipantId;
            m_Status.assistance = GetComponent<HintGenerator>()?.CaptureAssistanceStatus() ?? SessionConfig.CaptureAssistanceSelection();
#if UNITY_5_3_OR_NEWER
            UpdateMonitor(selector, message);
#endif
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
#if UNITY_5_3_OR_NEWER
            m_Game.OnReadinessEvent -= Readiness;
            m_Game.OnObjectFound -= Selected;
            m_Game.OnRoundTimedOut -= TimedOut;
            m_Game.OnCheckpoint -= Checkpoint;
            m_Game.OnWrongCapture -= WrongSelection;
#endif
        }
        m_Paused = true;
        Write();
    }
}
