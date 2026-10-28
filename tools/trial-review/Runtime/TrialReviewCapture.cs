using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Developer-only presentation harness. Fixtures never have a session folder or provider jobs.
public sealed partial class TrialReviewApp
{
    bool m_CaptureMode;
    string m_CaptureDirectory;
    void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--capture");
        m_CaptureMode = index >= 0;
        if (!m_CaptureMode) return;
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--"))
            throw new ArgumentException("--capture requires an output directory");
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
            id="development-capture-only", participantCode="P003", configuration="A", development=true,
            protocol="audio-intake-v2-20261028", phase=phase, neutralGender="Male", neutralOption=0,
            prompt="How often do you use virtual reality? Tell me about your experience and comfort with VR.",
            promptRevision=1, cloneState="not_started", cacheState="not_started", speechSeconds=18,
            recordedSeconds=25, sampleWords=41, cacheTotal=1132,
            answers=new [] {
                new IntakeAnswer{question="Are you at least 18 years old?",transcript="Yes.",audio="fixture-only.wav"},
                new IntakeAnswer{question="Can you hear and understand spoken guidance comfortably?",transcript="Yes."},
                new IntakeAnswer{question="Can you see the scene clearly with your usual correction?",transcript="Yes."},
                new IntakeAnswer{question="Can you safely remain seated, turn, point and use the right-controller trigger?",transcript="Yes."}
            }
        };
        if(phase!="awaiting_consent")state.consent=new IntakeConsent{id="fixture",sessionId=state.id,participantCode=state.participantCode,study=true,voiceCloud=true};
        if(phase=="interview_complete") {state.eligible=true;state.cloneState="ready";state.cacheState="preparing";state.cacheCompleted=420;state.speechSeconds=34;state.sampleWords=72;}
        return state;
    }
    IEnumerator CaptureScreens()
    {
        Directory.CreateDirectory(m_CaptureDirectory);
        m_Catalog=TrialReviewCatalog.DevelopmentFixture();
        m_Dataset=m_Catalog.Register(m_Path,"synthetic-desktop-demo","SYNTHETIC","");
        m_DatasetDraft="Synthetic development demo";m_IdDraft="SYNTHETIC";
        m_IntakeMic="Development microphone (inactive)";
        string[] states={"first-person","playing-subtitles","missing-pose","third-person","overhead","cursor-speech","events","final-no-summary","jev","evaluation","selected-trial","light-first-person","light-jev","open-run","dataset","empty","loading","intake-reserve","intake-consent","intake-voice","intake-interview","intake-eligibility","intake-cache","intake-ready","file-menu","layers"};
        var timeline=m_Timeline;
        for(int i=0;i<states.Length;i++)
        {
            string state=states[i];
            m_Timeline=timeline; m_Playing=false; m_Tab=0; m_CameraMode=0; m_RunTimeline=true;
            m_InspectScroll=Vector2.zero; m_IntakeScroll=Vector2.zero; m_IntakeOpen=false; m_Intake=null; m_IntakeFolder=null;
            m_LayersOpen=state=="layers";m_FileMenu=state=="file-menu";m_Browser=false;m_DatasetPanel=false;m_Loading=false;m_Ready=false;m_LightTheme=state.StartsWith("light-");
            Seek(0);
            if(state=="playing-subtitles"){Seek(1.2);m_Playing=true;}
            if(state=="missing-pose")Seek(8.35);
            if(state=="third-person")m_CameraMode=1;
            if(state=="overhead")m_CameraMode=2;
            if(state=="cursor-speech") {Seek(1.2);m_InspectScroll=new Vector2(0,180);}
            if(state=="events")m_Tab=1;
            if(state=="final-no-summary")m_Tab=2;
            if(state=="jev"||state=="light-jev"){m_Tab=3;m_SelectedJev=m_Tracks.Jev.Decisions.FirstOrDefault()?.Id;}
            if(state=="evaluation")m_Tab=4;
            if(state=="selected-trial")m_RunTimeline=false;
            if(state=="open-run"){m_Browser=true;m_BrowserPath=Path.Combine(Application.streamingAssetsPath,"Demo");m_Directories=Array.Empty<string>();m_Files=new[]{m_Path};}
            if(state=="dataset"){m_DatasetPanel=true;m_CatalogTab=0;}
            if(state=="empty")m_Timeline=null;
            if(state=="loading")m_Loading=true;
            if(state.StartsWith("intake-"))
            {
                m_IntakeOpen=true;m_IntakeDevelopment=true;m_IntakeCode="P003";
                string phase=state=="intake-consent"?"awaiting_consent":state=="intake-voice"?"select_voice":state=="intake-eligibility"?"eligibility_review":state=="intake-cache"||state=="intake-ready"?"interview_complete":"interview";
                if(state!="intake-reserve")m_Intake=CaptureIntake(phase);
                if(state=="intake-ready"){m_Intake.cacheState="ready";m_Intake.cacheCompleted=1132;}
                m_QuestionHeardRevision=1;
            }
            for(int frame=0;frame<3;frame++)yield return null;
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
