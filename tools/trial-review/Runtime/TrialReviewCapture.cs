using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Developer-only presentation harness. Fixtures never have a session folder or provider jobs.
public sealed partial class TrialReviewApp
{
    bool m_CaptureMode;
    bool m_RecordedSceneCapture;
    bool m_CaptureCheckInputs;
    string m_CaptureDirectory,m_CaptureIntakeActivity;
    void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--capture");
        int recordedIndex = Array.IndexOf(args, "--capture-recorded-scene");
        m_RecordedSceneCapture = recordedIndex >= 0;
        if(m_RecordedSceneCapture) index=recordedIndex;
        m_CaptureMode = index >= 0;
        if (!m_CaptureMode) return;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--"))
            throw new ArgumentException("Capture requires an output directory");
        m_CaptureDirectory = Path.GetFullPath(args[index + 1]);
        m_ThemeLoaded = true; m_LightTheme = false;
    }
    sealed class CaptureSilentOutput : TrialReplayAudioPreview.IOutput
    {
        public object Play(float[] samples, int channels, int rate, int offsetFrames, string name) => null;
        public void Stop(object handle) { }
    }
    IntakeState CaptureIntake(string phase)
    {
        var state = new IntakeState {
            id="development-capture-only", participantCode="P900000000003", configuration="A", development=true,
            protocol=IntakeProtocol, phase=phase, neutralGender="Male", neutralOption=0,
            prompt="Tell me about any experience you have had with virtual reality. What have you tried, how often, and how did it feel? If you have never tried it, tell me what you imagine it is like.",
            questionIndex=4, promptRevision=1, cloneState="not_started", cacheState="not_started", speechSeconds=18,
            recordedSeconds=25, sampleWords=41, cacheTotal=1132,
            answers=new [] {
                new IntakeAnswer{question="Are you at least 18 years old?",transcript="Yes.",audio="fixture-only.wav"},
                new IntakeAnswer{question="Can you hear and understand spoken guidance comfortably?",transcript="Yes."},
                new IntakeAnswer{question="Can you see the scene clearly with your usual correction?",transcript="Yes."},
                new IntakeAnswer{question="Can you safely remain seated, turn, point and use the right-controller trigger?",transcript="Yes."}
            }
        };
        if(phase=="awaiting_consent"||phase=="select_voice"){state.answers=Array.Empty<IntakeAnswer>();state.recordedSeconds=state.speechSeconds=0;state.sampleWords=0;}
        state.selectedVoiceConfirmed=phase!="awaiting_consent"&&phase!="select_voice";
        state.eligible=false;
        if(phase=="eligibility_review"||phase=="screening_stopped"||phase=="interview_complete"||phase=="fallback_available"||phase=="fallback")
        {
            state.answers=state.answers.Concat(new[]{
                new IntakeAnswer{question="How often have you used virtual reality?",transcript="Weekly, mostly seated experiences."},
                new IntakeAnswer{question="How often do you use voice assistants?",transcript="Daily, for reminders."},
                new IntakeAnswer{question="Have you used eye tracking before?",transcript="No."},
                new IntakeAnswer{question="Which hand do you usually prefer?",transcript="Right."},
                new IntakeAnswer{question="What visual correction are you using?",transcript="Glasses."},
                new IntakeAnswer{question="Imagine you are helping a friend find something they lost in a room. Talk them through it for about thirty seconds, telling them where to look, which way to turn, and when they are getting close.",transcript="Turn left and look near the chair. You are getting close."}
            }).ToArray();
            if(phase!="fallback_available"&&phase!="fallback"){state.speechSeconds=34;state.sampleWords=72;state.recordedSeconds=42;}
        }
        if(phase=="fallback")state.prompt="Read the following passage in your natural voice after the tone. Locate the red sphere. Look toward the upper shelf. Move your gaze slowly to the right.";
        if(phase!="awaiting_consent")state.consent=new IntakeConsent{id="fixture",sessionId=state.id,participantCode=state.participantCode,study=true,voiceCloud=true};
        if(phase=="interview_complete") {state.eligible=true;state.eligibility=new IntakeEligibility{id="fixture-review"};state.cloneState="ready";state.cacheState="preparing";state.cacheCompleted=420;}
        string[] exactQuestions={
            "Are you 18 years of age or older? After the tone, please answer in a full sentence.",
            "Can you hear and understand this voice at a comfortable volume? After the tone, please answer in a full sentence.",
            "With your usual glasses or contact lenses if needed, can you see clearly enough to read and to find small objects? After the tone, please answer in a full sentence.",
            "Can you safely stay seated, turn in a swivel chair, and use a controller in your right hand? After the tone, please answer in a full sentence. You do not need to give medical details.",
            "Tell me about any experience you have had with virtual reality. What have you tried, how often, and how did it feel? If you have never tried it, tell me what you imagine it is like.",
            "Tell me how you use voice assistants or spoken directions in everyday life, such as on your phone or while driving. What works well for you, and what is frustrating?",
            "Have you used eye tracking before? If you have, describe it. If not, how would you feel about a system that knows where you are looking?",
            "Which hand do you prefer for everyday tasks, and what is something you always do with that hand?",
            "Are you wearing glasses, contact lenses, or no visual correction today? Tell me how they usually feel when you are looking at screens. No prescription details are needed.",
            "Imagine you are helping a friend find something they lost in a room. Talk them through it for about thirty seconds, telling them where to look, which way to turn, and when they are getting close."
        };
        string[] topics={"adult","hearing","vision","seated_controller","vr","voice_assistant","eye_tracking","handedness","visual_correction","directional_speech"};
        for(int i=0;i<state.answers.Length;i++)
        {state.answers[i].id="fixture-"+i;state.answers[i].question=exactQuestions[i];state.answers[i].topic=topics[i];state.answers[i].structuredValue="unclear";state.answers[i].transcriptionStatus="complete";}
        return state;
    }
    void RunCaptureInputChecks()
    {
        var original=Event.current;
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException("Capture input check: "+message);}
        try
        {
            m_Playing=false;m_SpaceHeld=false;Seek(0);
            void Key(EventType type,KeyCode code,bool enabled=true,string focus="")=>HandlePlaybackKeyboard(new Event{type=type,keyCode=code},enabled,focus);
            Key(EventType.KeyDown,KeyCode.Space);Check(m_Playing,"Space plays");
            Key(EventType.KeyDown,KeyCode.Space);Check(m_Playing,"Held Space does not repeat");
            Key(EventType.KeyUp,KeyCode.Space);Key(EventType.KeyDown,KeyCode.Space);Check(!m_Playing,"Space pauses");
            Key(EventType.KeyUp,KeyCode.Space);
            Key(EventType.KeyDown,KeyCode.RightArrow);Check(m_Time>0,"Right steps forward");
            Key(EventType.KeyDown,KeyCode.LeftArrow);Check(m_Time==0,"Left steps backward");
            Key(EventType.KeyDown,KeyCode.Space,true,"text:trial-search");Check(!m_Playing,"Text field retains Space");
            Key(EventType.KeyDown,KeyCode.Space,false);Check(!m_Playing,"Modal retains Space");
            var area=new Rect(100,100,400,138);int id=GUIUtility.GetControlID(FocusType.Passive);var trial=m_Timeline.Trials[0];
            void Pointer(EventType type,float x)
            {
                Event.current=new Event{type=type,button=0,mousePosition=new Vector2(x,160)};
                HandleTimelinePointer(area,0,60,trial,id);
            }
            Pointer(EventType.MouseDown,200);Check(Math.Abs(m_Time-15)<.001&&m_TimelineDrag,"Drag starts at cursor");
            Pointer(EventType.MouseDrag,600);Check(m_Time==60,"Drag clamps at end across trial boundaries");
            Pointer(EventType.MouseUp,400);Check(Math.Abs(m_Time-45)<.001&&!m_TimelineDrag&&GUIUtility.hotControl==0,"Drag releases at cursor");
            // Pointer checks crossed trials in a single GUI frame. Start screenshots with a fresh renderer,
            // so display-only collider caches cannot retain objects from the exercised seek sequence.
            m_Renderer.Dispose();m_Renderer=new TrialReplayRenderer(m_Timeline){RecordedHead=true,ShowGaze=false};
            ResetCamera();Seek(0);m_CaptureCheckInputs=false;
            Debug.Log("TRIAL_REVIEW_CAPTURE_INPUT_PASS Space, repeat guard, arrows, text/modal guards, timeline drag and clamp");
        }
        finally {Event.current=original;}
    }
    IEnumerator CaptureScreens()
    {
        Directory.CreateDirectory(m_CaptureDirectory);
        m_Catalog=TrialReviewCatalog.DevelopmentFixture();
        m_Dataset=m_Catalog.Register(m_Path,"synthetic-desktop-demo","SYNTHETIC","");
        m_DatasetDraft="Synthetic development demo";m_IdDraft="SYNTHETIC";
        m_IntakeMic="Development microphone (inactive)";
        string[] states={"first-person","playing-subtitles","missing-pose","third-person","overhead","cursor-speech","events","final-no-summary","jev","evaluation","selected-trial","light-first-person","light-jev","open-run","dataset","empty","loading","intake-reserve","intake-consent","intake-voice","intake-interview","intake-eligibility","intake-cache","intake-ready","file-menu","layers","trial-search","cursor-details","cursor-collapsed","shortcuts","jev-alternatives","light-intake-consent","light-intake-interview","intake-clone-error","intake-screening-stopped","intake-withdrawn","intake-historical","intake-recording","intake-submitting","intake-prompt-playing","startup","layers-wifi-panel","intake-fallback-available","intake-fallback","light-intake-eligibility","intake-reviewed-awaiting-clone","intake-paused","intake-transcription-failed","intake-identifier-flag","intake-directional-speech","intake-reserve-study","intake-reserve-duplicate","intake-fit-change","intake-fit-no-change","intake-fit-missing"};
        var timeline=m_Timeline;
        states=states.Concat(new[]{"intake-fit-fallback"}).ToArray();
        for(int i=0;i<states.Length;i++)
        {
            string state=states[i];
            m_LiveStatus=null;
            m_Timeline=timeline; m_Playing=false; m_Tab=0; m_CameraMode=0; m_RunTimeline=true;
            m_JevScroll=Vector2.zero;m_InspectScroll=Vector2.zero; m_IntakeScroll=Vector2.zero; m_CaptureIntakeActivity=null;m_IntakePaused=false;m_WithdrawRequested=false;m_EditAnswerId=null;m_IntakeOpen=false; m_Intake=null; m_IntakeFolder=null;
            m_TrialQuery=state=="trial-search"?"02":"";m_LivePanel=state=="layers-wifi-panel";m_LayersScroll=m_LivePanel?new Vector2(0,400):Vector2.zero;m_LayersOpen=state=="layers"||m_LivePanel;m_FileMenu=state=="file-menu";m_Browser=false;m_DatasetPanel=false;m_Loading=false;m_Ready=false;m_LightTheme=state.StartsWith("light-");
            m_ShortcutsOpen=state=="shortcuts";m_CollapsedCards.Clear();
            if(state=="cursor-details")m_InspectScroll=new Vector2(0,500);
            if(state=="cursor-collapsed")foreach(string card in new[]{"Agent speech","Guidance state","Tracking"})m_CollapsedCards[card]=true;
            Seek(0);
            if(state=="playing-subtitles"){Seek(1.2);m_Playing=true;}
            if(state=="missing-pose")Seek(8.35);
            if(state=="third-person")m_CameraMode=1;
            if(state=="overhead")m_CameraMode=2;
            if(state=="cursor-speech") {Seek(1.2);m_InspectScroll=Vector2.zero;}
            if(state=="events")m_Tab=1;
            if(state=="final-no-summary")m_Tab=2;
            if(state=="jev"||state=="light-jev"||state=="jev-alternatives"){m_Tab=3;m_SelectedJev=m_Tracks.Jev.Decisions.FirstOrDefault()?.Id;}
            if(state=="jev-alternatives")m_JevScroll=new Vector2(0,330);
            if(state=="evaluation")m_Tab=4;
            if(state=="selected-trial")m_RunTimeline=false;
            if(state=="open-run"){m_Browser=true;m_BrowserPath=Path.Combine(Application.streamingAssetsPath,"Demo");m_Directories=Array.Empty<string>();m_Files=new[]{m_Path};}
            if(state=="dataset"){m_DatasetPanel=true;m_CatalogTab=0;}
            if(state=="empty")m_Timeline=null;
            if(state=="loading")m_Loading=true;
            if(state=="startup"){m_Timeline=null;m_Loading=true;}
            string intakeState=state.StartsWith("light-")?state.Substring(6):state;
            if(intakeState.StartsWith("intake-"))
            {
                m_IntakeOpen=true;m_IntakeDevelopment=true;m_IntakeCode="P900000000003";m_CheckedCode=m_IntakeCode;m_CodeDuplicate=false;m_CodeError=null;
                string phase=intakeState=="intake-consent"?"awaiting_consent":intakeState=="intake-voice"?"select_voice":intakeState=="intake-eligibility"?"eligibility_review":intakeState=="intake-cache"||intakeState=="intake-ready"||intakeState=="intake-clone-error"?"interview_complete":"interview";
                if(intakeState=="intake-screening-stopped")phase="screening_stopped";
                if(intakeState=="intake-fallback-available")phase="fallback_available";
                if(intakeState=="intake-fallback")phase="fallback";
                if(intakeState=="intake-reviewed-awaiting-clone")phase="interview_complete";
                if(!intakeState.StartsWith("intake-reserve"))m_Intake=CaptureIntake(phase);
                if(intakeState=="intake-reserve-study" || intakeState=="intake-reserve-duplicate")
                {m_IntakeDevelopment=false;m_IntakeCode=m_CheckedCode="P015";m_CodeDuplicate=intakeState.EndsWith("duplicate");}
                if(intakeState.StartsWith("intake-fit-"))
                {
                    m_Intake=CaptureIntake("awaiting_consent");m_IntakeScroll=new Vector2(0,1000);
                    m_Intake.fit=new ParticipantFitMeasurements {autoIpdStatus="unavailable",fitEndedUtc="2026-11-01T15:00:00Z",
                        separationChange=intakeState=="intake-fit-change"?"change_observed":"no_change_observed",separationChangeMm=1.2f,separationChangeUtc="2026-11-01T14:59:57Z",
                        viewSeparation=new FitSeparationMeasurement{recorded=true,millimetres=64.2f,samples=42,windowSeconds=2.15f,rangeMm=.2f,source="Unity_OpenXR_head_leftEyePosition_rightEyePosition",recordedUtc="2026-11-01T15:00:00Z"},
                        eyeSeparation=new FitSeparationMeasurement{recorded=true,millimetres=63.8f,samples=42,windowSeconds=2.15f,rangeMm=.4f,source="XR_HTC_eye_tracker_gazePose_position",recordedUtc="2026-11-01T15:00:00Z"}};
                    if(intakeState=="intake-fit-missing"||intakeState=="intake-fit-fallback")m_Intake.fit=new ParticipantFitMeasurements{fitEndedUtc="2026-11-01T15:00:00Z",fitWarning="Missing or implausible separation; inspect fit or use manual fallback."};
                    if(intakeState=="intake-fit-fallback")
                    {
                        m_IntakeIpd="63.5";m_IntakeIpdSource=1;m_IntakeHeadsetIpd="64";
                        m_LiveStatus=new LiveStatus{state="connected",updatedUtc=UtcSeconds,headsetStateObservedUtc=UtcSeconds,
                            headsetState=new TrialReviewHeadsetState{schema=1,sessionId="synthetic-fit",revision=10,phase="ready",updatedUtc=UtcSeconds,
                                participantSessionId=m_Intake.id,participantCode=m_Intake.participantCode,fit=m_Intake.fit}};
                    }
                }
                if(intakeState=="intake-reviewed-awaiting-clone"){m_Intake.cloneState="not_started";m_Intake.cacheState="not_started";m_Intake.cacheCompleted=0;}
                if(intakeState=="intake-ready"){m_Intake.cacheState="ready";m_Intake.cacheCompleted=1132;}
                if(intakeState=="intake-clone-error"){m_Intake.cloneState="uncertain";m_Intake.cacheState="not_started";m_Intake.cacheCompleted=0;m_Intake.cloneError="Development fixture: interrupted clone request. Review the provider result before retrying.";}
                if(intakeState=="intake-screening-stopped"){m_Intake.phase="screening_stopped";m_Intake.eligible=false;}
                if(intakeState=="intake-withdrawn"){m_Intake.phase="withdrawn";m_Intake.withdrawnUtc=1;}
                if(intakeState=="intake-historical")m_Intake.protocol="historical-fixture";
                if(intakeState=="intake-recording")m_CaptureIntakeActivity="Listening · speech detected · silence 2.0s / 5s";
                if(intakeState=="intake-submitting")m_CaptureIntakeActivity="Submitting";
                if(intakeState=="intake-prompt-playing")m_CaptureIntakeActivity="Prompt playing";
                if(intakeState=="intake-paused"){m_IntakePaused=true;m_CaptureIntakeActivity="Paused · microphone off";}
                if(intakeState=="intake-transcription-failed"||intakeState=="intake-identifier-flag")
                {
                    m_Intake=CaptureIntake("eligibility_review");m_TranscriptIndex=0;
                    if(intakeState=="intake-transcription-failed"){m_Intake.answers[0].transcriptionStatus="failed";m_Intake.answers[0].transcriptionError="Synthetic offline fixture";m_Intake.answers[0].transcript="";}
                    else{m_Intake.answers[0].possibleIdentifiers=true;m_Intake.answers[0].transcript="My name is Alex (synthetic fixture). I am over eighteen.";}
                    m_IntakeScroll=new Vector2(0,450);
                }
                if(intakeState=="intake-directional-speech"){m_Intake.questionIndex=9;m_Intake.prompt=CaptureIntake("eligibility_review").answers[9].question;}
                m_QuestionHeardRevision=1;
            }
            for(int frame=0;frame<3;frame++)yield return null;
            if(intakeState.StartsWith("intake-fit-"))
            {m_IntakeScroll=new Vector2(0,1000);for(int frame=0;frame<3;frame++)yield return null;}
            if(intakeState=="intake-transcription-failed"||intakeState=="intake-identifier-flag")
            {m_IntakeScroll=new Vector2(0,520);for(int frame=0;frame<3;frame++)yield return null;}
            yield return new WaitForEndOfFrame();
            string path=Path.Combine(m_CaptureDirectory,$"{i+1:00}-{state}.png");
            ScreenCapture.CaptureScreenshot(path);
            for(int frame=0;frame<3;frame++)yield return null;
            Debug.Log("TRIAL_REVIEW_CAPTURE "+path);
        }
        m_Timeline=timeline;m_Loading=false;m_IntakeOpen=false;
        // Dispose while Unity objects still exist, before shutdown destroys them in arbitrary order.
        m_Vicinity.Dispose();m_Environment?.Dispose();m_Environment=null;
        m_Actors?.Dispose();m_Actors=null;m_Audio?.Dispose();m_Audio=null;m_Renderer?.Dispose();m_Renderer=null;
        enabled=false;
        Debug.Log("TRIAL_REVIEW_CAPTURE_PASS "+states.Length+" states; no services or participant folders");
        Application.Quit();
    }
}
