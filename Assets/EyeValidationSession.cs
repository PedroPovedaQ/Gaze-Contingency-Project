using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Isolated, auditable measurement exercises. Completion is never physiological approval.</summary>
[DefaultExecutionOrder(-150)]
public sealed partial class EyeValidationSession : MonoBehaviour
{
    public static EyeValidationSession Instance { get; private set; }
    public EyeValidationStatus Status { get; } = new EyeValidationStatus();
    public bool Active => Status.active;
    public bool Available
    {
        get
        {
            var game=FindFirstObjectByType<FindObjectGameManager>();
            var voice=FindFirstObjectByType<VoiceModeSelector>();
            return !Active && Application.isFocused && Camera.main!=null && m_Stream!=null && m_Stream.RecordingError==null &&
                !EntryGazeAlignment.IsShowing && !(FindFirstObjectByType<ActivationExercise>()?.Active??false) &&
                (game==null || game.CurrentState!=FindObjectGameManager.GameState.Playing) &&
                voice!=null && voice.CanStageDesktopSession && !(FindFirstObjectByType<VoiceSynthesizer>()?.IsBusy??false);
        }
    }
    SharedGazeStream m_Stream;
    FindObjectGameManager m_Game;
    ControllerRaySelector m_Controller;
    bool m_OwnController;
    EyeValidationStep[] m_Steps;
    EyeValidationProgress m_Progress;
    EyeValidationRequest m_Request;
    TrialReplayWriter m_Events, m_Samples;
    string m_Directory;
    long m_Rows;
    bool m_Rating, m_BlinkCued, m_TriggerHeld, m_Ending;
    double m_LastCheckpoint;
    readonly HashSet<string> m_RawFiles=new HashSet<string>();
    static double Now => Time.realtimeSinceStartupAsDouble;
    EyeValidationStep Step => m_Steps[m_Progress.index];
    [Serializable] sealed class Manifest
    {
        public int schema=1;
        public string protocol=EyeValidationProtocol.Version, id, utc, build=Application.buildGUID,
            device=SystemInfo.deviceModel, unity=Application.unityVersion;
        public string timing="All event time values are Unity monotonic receipt/update seconds. Stimulus markers are software presentation requests, not photodiode-verified display onset or eye movement ground truth.";
        public string status="in_progress", error;
        public bool measurementsValidated=false;
        public EyeValidationRequest request;
        public ParticipantFitMeasurements participantFit;
        public EyeValidationStep[] schedule;
        public string[] rawFiles;
        public long sampleRows;
        public Vector3 worldAnchor;
        public Quaternion worldHeading;
        public string nativeRecordingError;
        public GazeQuickAlignment.AlignmentReport alignment;
        public StudyGazeAccuracy.Result studyAccuracy;
    }
    [Serializable] sealed class Marker
    {
        public string kind="validation_event", eventType, validationId, exercise, condition, stage, source, detail;
        public int step, revision, frame, value, expectedAnswer;
        public bool valueRecorded;
        public double time;
        public Vector3 targetPosition;
    }
    Manifest m_Manifest;
    void Awake(){Instance=this;m_Stream=GetComponent<SharedGazeStream>();m_Stream.Observed+=Observe;}
    public bool Command(string action,EyeValidationRequest request)
    {
        if(request==null)return false;
        if(action=="validation_start")return Begin(request);
        if(!Active || request.id!=Status.id)return false;
        // Stop is session-bound but can always interrupt a running exercise.
        if(action=="validation_stop"){End("aborted","Stopped from Trial Viewer");return true;}
        if(m_Alignment!=null)return false; // Calibration requires the wearer's own Start/OK.
        if(request.revision!=m_Progress.revision)return false;
        if(action=="validation_next")return Next("trial_viewer");
        if(action=="validation_answer")return Respond(request.value,"trial_viewer");
        return false;
    }
    // Internal study entry point; remote commands cannot opt into retained study alignment.
    public bool BeginStudyAccuracy()
    {
        if(Active || !Application.isFocused || Camera.main==null || m_Stream==null || m_Stream.RecordingError!=null ||
            !m_Stream.QuickAlignment.AwaitingAccuracy || m_Stream.QuickAlignment.Report?.participant!=SessionConfig.ParticipantId)return false;
        var game=FindFirstObjectByType<FindObjectGameManager>();
        if(game==null || game.CurrentState!=FindObjectGameManager.GameState.Idle)return false;
        return Begin(new EyeValidationRequest {mode="drift",participant=SessionConfig.ParticipantId,
            fit=SessionConfig.ParticipantFit?.Copy(),notes="Standard pre-experiment independent accuracy check"},true);
    }
    bool m_StudyAccuracy;
    StudyGazeAccuracy m_Accuracy;
    public StudyGazeAccuracy.Result StudyAccuracyResult {get;private set;}
    public void AbortStudyAccuracy(){if(Active && m_StudyAccuracy)End("aborted","Study preparation interrupted");}
    bool Begin(EyeValidationRequest request,bool studyAccuracy=false)
    {
        bool participantAllowed=studyAccuracy
            ? !string.IsNullOrWhiteSpace(request.participant) && request.participant==SessionConfig.ParticipantId
            : Regex.IsMatch(request.participant??"","^P[0-9]{3,12}$");
        if((!studyAccuracy && !Available) || !participantAllowed || (request.notes??"").Length>500)return false;
        EyeValidationStep[] schedule;
        try { request.fit?.Validate(); schedule=EyeValidationProtocol.Build(request.mode,request.seed,request.hardFirst); }
        catch(ArgumentException) { return false; }
        m_StudyAccuracy=studyAccuracy;m_Accuracy=studyAccuracy?new StudyGazeAccuracy(
            new Vector2(m_Stream.QuickAlignment.Report.yawCorrectionDegrees,m_Stream.QuickAlignment.Report.pitchCorrectionDegrees).magnitude):null;StudyAccuracyResult=null;
        m_Manifest=null;m_Directory=null;
        try
        {
            m_Steps=schedule;
            GazeDiagnosticView.Instance?.SetVisible(false);
            m_Stream.SealRecording();
            m_Request=request;m_Rows=0;m_RawFiles.Clear();m_Rating=false;m_LastCheckpoint=Now;
            Status.id=Guid.NewGuid().ToString("N");Status.participant=request.participant;Status.completed=false;Status.error="";
            m_Directory=Path.Combine(Application.persistentDataPath,"EyeMeasurements","validation_"+DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"_"+Status.id);
            Directory.CreateDirectory(m_Directory);
            m_Manifest=new Manifest {id=Status.id,utc=DateTime.UtcNow.ToString("O"),request=request,schedule=m_Steps,
                participantFit=request.fit?.Copy() ?? (request.participant==SessionConfig.ParticipantId?SessionConfig.ParticipantFit?.Copy():new ParticipantFitMeasurements())};
            SaveManifest("in_progress","");
            m_Events=new TrialReplayWriter(Path.Combine(m_Directory,"events.jsonl"));
            m_Samples=new TrialReplayWriter(Path.Combine(m_Directory,"samples.jsonl"));
            m_Progress=new EyeValidationProgress(m_Steps.Length,Now);
            Status.active=true;Status.recordingPath=m_Directory;
            m_Game=FindFirstObjectByType<FindObjectGameManager>();m_Controller=FindFirstObjectByType<ControllerRaySelector>();
            // Validation is available before study setup creates its controller selector.
            m_OwnController=m_Controller==null;
            if(m_OwnController)m_Controller=gameObject.AddComponent<ControllerRaySelector>();
            Event("session_start",studyAccuracy?"study_setup":"trial_viewer",request.notes);
            if(request.alignFirst)BeginAlignment();else BeginExercises();
            return true;
        }
        catch(Exception e){End("error",e.Message);return false;}
    }
    void SaveManifest(string status,string error)
    {
        if(m_Manifest==null)return;
        m_Manifest.nativeRecordingError=NativeGazeRecorder.Instance?.RecordingError;
        m_Manifest.status=status;m_Manifest.error=error;m_Manifest.rawFiles=new List<string>(m_RawFiles).ToArray();m_Manifest.sampleRows=m_Rows;
        string path=Path.Combine(m_Directory,"manifest.json"),temp=path+".tmp";
        File.WriteAllText(temp,JsonUtility.ToJson(m_Manifest,true));
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    double Event(string type,string source="headset",string detail="",int value=0,bool recorded=false)
    {
        var s=m_Progress.index<m_Steps.Length?Step:null;
        var marker=new Marker {eventType=type,validationId=Status.id,exercise=s?.exercise,condition=s?.condition,
            stage=m_Alignment!=null?"alignment":m_Progress.stage,step=m_Progress.index,revision=m_Progress.revision,time=Now,frame=Time.frameCount,
            source=source,detail=detail,value=value,valueRecorded=recorded,expectedAnswer=s?.answer??0,
            targetPosition=m_Dot!=null?m_Dot.transform.position:Vector3.zero};
        if(m_Events!=null && !m_Events.AppendJson(JsonUtility.ToJson(marker)))throw new IOException(m_Events.Error??"Validation event writer stopped");
        m_Events?.RequestFlush();
        return marker.time;
    }
    void EnterStep()
    {
        m_Rating=false;m_BlinkCued=false;ClearSearch();
        if(m_Progress.stage=="complete"){End("complete","");return;}
        UpdateStatus();RenderStage();Event("instruction");
    }
    bool Next(string source)
    {
        if(!Active || !m_Progress.Next(m_Progress.revision,Now))return false;
        RenderStage();
        double onset=Event("stimulus_start",source);
        m_Accuracy?.StartPoint(m_Progress.index,onset);
        UpdateStatus();return true;
    }
    bool Respond(int value,string source)
    {
        if(!Active || m_Progress.stage!="response")return false;
        if(m_Rating && (value<1 || value>7))return false;
        if(!m_Rating && (value<0 || value>999))return false;
        Event(m_Rating?"difficulty_rating":"answer",source,"",value,true);
        if(!m_Rating && Step.rateAfter)
        {
            m_Rating=true;m_Progress.revision++;m_Progress.since=Now;RenderStage();UpdateStatus();Event("rating_prompt");return true;
        }
        m_Progress.Answer(m_Progress.revision,Now);EnterStep();return true;
    }
    void FinishStimulus()
    {
        string previous=Step.exercise;
        m_Accuracy?.EndPoint(m_Progress.index);
        Event("stimulus_end");m_Progress.FinishStimulus(Now,Step.response);
        if(m_Progress.stage=="response"){RenderStage();UpdateStatus();Event("response_prompt");return;}
        EnterStep();
        // Dot/control sequences have one instruction screen per exercise, not per target.
        if(Active && Step.exercise==previous && Step.exercise!="search")
        {m_Progress.since=Now-2;Next("automatic_sequence");}
    }
    void Update()
    {
        if(!Active)return;
        try
        {
            if(m_Game!=null && m_Game.CurrentState==FindObjectGameManager.GameState.Playing){End("interrupted","Study started");return;}
            if(m_Stream.RecordingError!=null || m_Events.Error!=null || m_Samples.Error!=null)
            {End("error",m_Stream.RecordingError??m_Events.Error??m_Samples.Error);return;}
            if(m_Alignment!=null){UpdateAlignment();return;}
            if((m_Request.alignFirst || m_StudyAccuracy) && !m_Stream.QuickAlignment.IsActive && !m_Stream.QuickAlignment.AwaitingAccuracy)
            {End("interrupted","Alignment was cleared; repeat alignment before checking accuracy.");return;}
            if(m_StudyAccuracy && m_Progress.stage=="instruction" && Now-m_Progress.since>=3)Next("automatic_study_verification");
            if(m_Progress.stage=="instruction" && Step.exercise=="arithmetic" && Now-m_Progress.since>=4)Next("timed_prompt");
            if(m_Progress.stage=="running")
            {
                MoveStimulus();
                if(Step.exercise=="blinks" && !m_BlinkCued && Now-m_Progress.since>=4){m_BlinkCued=true;Event("blink_cue");}
                if(Now-m_Progress.since>=Step.seconds)FinishStimulus();
            }
            else if(m_Progress.stage=="response" && Now-m_Progress.since>=20)
            {
                Event(m_Rating?"difficulty_missing":"answer_missing","timeout");
                if(!m_Rating && Step.rateAfter){m_Rating=true;m_Progress.revision++;m_Progress.since=Now;RenderStage();Event("rating_prompt");}
                else {m_Progress.Answer(m_Progress.revision,Now);EnterStep();}
            }
            if(Active && Now-m_LastCheckpoint>=5){SaveManifest("in_progress","");m_LastCheckpoint=Now;}
            UpdateStatus();UpdateView();
        }
        catch(Exception e){End("error",e.Message);}
    }
    void UpdateStatus()
    {
        if(!Active)return; // Preserve the terminal receipt, including writer failures during completion.
        Status.stage=m_Rating?"rating":m_Progress.stage;Status.revision=m_Progress.revision;Status.step=m_Progress.index;
        Status.count=m_Steps.Length;
        if(m_Progress.index>=m_Steps.Length)return;
        Status.exercise=Step.exercise;Status.condition=Step.condition;
        Status.prompt=m_Rating?"How difficult was that block? 1 = very easy, 7 = very difficult":Step.prompt;
        Status.remaining=m_Progress.stage=="running"?Mathf.Max(0,Step.seconds-(float)(Now-m_Progress.since)):0;
    }
    public void Annotate(GazeObservation sample)
    {
        if(!Active)return;
        sample.validationId=Status.id;sample.validationSet=m_Alignment!=null?"alignment":Step.exercise;
        sample.validationStage=m_Alignment!=null?"alignment":m_Progress.stage;sample.validationStep=m_Progress.index;sample.validationCondition=Step.condition;
        sample.validationProtocol=EyeValidationProtocol.Version;sample.participant=Status.participant;
        sample.phase="validation";sample.sourceTrialId="";sample.round=-1;sample.block=Step.block;sample.run=0;sample.voice="";sample.practice=true;
        sample.taskAoiAvailable=false;sample.taskTargetId=sample.leftHitObjectId=sample.rightHitObjectId="";
        if(m_Alignment!=null)return; // Preserve the separate fit/check target geometry.
        sample.validationTargetPresent=m_Progress.stage=="running";
        sample.targetId="validation_"+m_Progress.index;
        if(sample.validationTargetPresent && m_Dot!=null)
        {
            sample.validationTarget=m_Dot.transform.position;
            sample.targetErrorDegrees=Vector3.Angle(sample.rawWorldDirection,sample.validationTarget-sample.rawWorldOrigin);
            sample.usedTargetErrorDegrees=sample.valid?Vector3.Angle(sample.worldDirection,sample.validationTarget-sample.worldOrigin):0;
        }
    }
    void Observe(GazeObservation sample)
    {
        if(!Active)return;
        if(!m_Samples.Append(sample)){End("error",m_Samples.Error??"Sample writer stopped");return;}
        m_Accuracy?.Observe(sample,sample.validationStage=="running"?sample.validationStep:-1);
        m_Rows++;
        if(!string.IsNullOrEmpty(m_Stream.RecordingPath))m_RawFiles.Add(m_Stream.RecordingPath);
        if(!string.IsNullOrEmpty(NativeGazeRecorder.Instance?.RecordingPath))m_RawFiles.Add(NativeGazeRecorder.Instance.RecordingPath);
        if(m_Rows%120==0)m_Samples.RequestFlush();
    }
    void End(string outcome,string reason)
    {
        if(m_Ending)return;m_Ending=true;
        try
        {
            if(m_Alignment!=null)
            {
                m_Alignment.Abort();
                m_Manifest.alignment=m_Stream.QuickAlignment.Report;
            }
            if(Active)Event("session_end","headset",outcome+": "+reason);
        }catch(Exception e){outcome="error";reason=e.Message;}
        Status.active=false;Status.completed=outcome=="complete";Status.stage=outcome;Status.error=reason;
        try
        {
            m_Events?.Dispose();m_Samples?.Dispose();
            if(m_Events?.Error!=null || m_Samples?.Error!=null){Status.stage="error";Status.completed=false;Status.error=m_Events?.Error??m_Samples?.Error;}
            m_Stream?.SealRecording();
            if(m_Stream?.RecordingError!=null){Status.stage="error";Status.completed=false;Status.error=m_Stream.RecordingError;}
            if(m_Accuracy!=null)
            {
                StudyAccuracyResult=m_Accuracy.Summarize();
                StudyAccuracyResult.passed &= Status.completed && m_Stream.QuickAlignment.AwaitingAccuracy;
                if(StudyAccuracyResult.passed)
                    StudyAccuracyResult.passed=m_Stream.QuickAlignment.ConfirmStudyAccuracy(StudyAccuracyResult,Status.id,Now);
                m_Manifest.studyAccuracy=StudyAccuracyResult;
                m_Manifest.alignment=m_Stream.QuickAlignment.Report;
            }
            SaveManifest(Status.stage,Status.error);
        }catch(Exception e){Status.stage="error";Status.completed=false;Status.error=e.Message;Debug.LogError("[EyeValidation] "+e.Message);}
        finally
        {
            m_Events=null;m_Samples=null;ReleaseAlignment();DestroyView();
            if(m_StudyAccuracy && (Status.stage!="complete" || StudyAccuracyResult?.passed!=true))
                m_Stream?.ClearQuickAlignment("Study accuracy check failed or interrupted; retry or explicitly skip.");
            if(m_Request?.alignFirst==true)m_Stream?.ClearQuickAlignment("Validation-only alignment ended; align again for study setup.");
            if(m_OwnController && m_Controller!=null){m_Controller.enabled=false;Destroy(m_Controller);}
            m_Controller=null;m_OwnController=false;m_Ending=false;
        }
    }
    void OnApplicationPause(bool paused){if(paused && Active)End("interrupted","Headset paused");}
    void OnApplicationFocus(bool focused){if(!focused && Active)End("interrupted","Application lost focus");}
    void OnDestroy(){if(Active)End("interrupted","Component destroyed");if(m_Stream!=null)m_Stream.Observed-=Observe;if(Instance==this)Instance=null;}
}
