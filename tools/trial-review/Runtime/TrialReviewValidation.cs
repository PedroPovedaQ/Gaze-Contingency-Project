using System;
using System.IO;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    bool m_ValidationOpen,m_ValidationHardFirst,m_ValidationAlignFirst;
    int m_ValidationMode;
    string m_ValidationParticipant="P001",m_ValidationNotes="",m_ValidationAnswer="";
    Vector2 m_ValidationScroll;
    string m_ValidationIpd="",m_ValidationLens="";
    int m_ValidationIpdSource;
    void SetValidationParticipant(string participant)
    {
        if(participant==m_ValidationParticipant)return;
        m_ValidationParticipant=participant;
        m_ValidationIpd=m_ValidationLens=m_ValidationNotes=m_ValidationAnswer="";
        m_ValidationIpdSource=0;
    }
    ParticipantFitMeasurements ValidationFit()
    {
        var fit=new ParticipantFitMeasurements {ipdRecorded=!string.IsNullOrWhiteSpace(m_ValidationIpd),
            headsetIpdRecorded=!string.IsNullOrWhiteSpace(m_ValidationLens),ipdSource=IpdSources[m_ValidationIpdSource]};
        if(fit.ipdRecorded && !float.TryParse(m_ValidationIpd,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out fit.participantIpdMm))
            throw new ArgumentException("Enter participant IPD in millimetres using a decimal point.");
        if(fit.headsetIpdRecorded && !float.TryParse(m_ValidationLens,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out fit.headsetIpdMm))
            throw new ArgumentException("Enter lens spacing in millimetres using a decimal point.");
        fit.Validate();return fit;
    }
    void OpenValidation()
    {
        m_ValidationOpen=true;m_IntakeOpen=false;
        var state=m_LiveStatus?.headsetState;
        SetValidationParticipant(!string.IsNullOrEmpty(state?.participantCode)?state.participantCode:state?.nextParticipantCode??"P001");
    }
    void SendValidation(string action,int value=0)
    {
        try
        {
            var state=m_LiveStatus.headsetState;
            var command=new HeadsetCommand {id=Guid.NewGuid().ToString("N"),sessionId=state.sessionId,action=action,
                observedRevision=state.revision,observedUtc=state.updatedUtc,createdUtc=UtcSeconds,
                validation=new EyeValidationRequest {id=state.validation?.id??"",revision=state.validation?.revision??0,
                    mode=EyeValidationProtocol.Modes[m_ValidationMode],participant=m_ValidationParticipant.Trim(),notes=m_ValidationNotes,
                    seed=Guid.NewGuid().GetHashCode(),hardFirst=m_ValidationHardFirst,alignFirst=m_ValidationAlignFirst,value=value,fit=action=="validation_start"?ValidationFit():null}};
            string path=Path.Combine(m_LiveOutput,"headset-command.json");
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(command));
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
            m_HeadsetCommand=command;m_ControlError=null;
        }catch(Exception e){m_ControlError="Validation control: "+e.Message;}
    }
    void DrawValidation(float width,float height)
    {
        if(!m_ValidationOpen)return;
        var rect=new Rect(Mathf.Max(12,(width-850)/2),40,Mathf.Min(850,width-24),height-80);
        Panel(rect);GUILayout.BeginArea(new Rect(rect.x+18,rect.y+18,rect.width-36,rect.height-36));
        GUILayout.BeginHorizontal();GUILayout.Label("Eye measurement validation",m_Heading);GUILayout.FlexibleSpace();
        if(Button("Close",GUILayout.Width(90)))m_ValidationOpen=false;GUILayout.EndHorizontal();
        m_ValidationScroll=GUILayout.BeginScrollView(m_ValidationScroll);
        GUILayout.Label("Separate from study trials. Completion means recordings finished; it does not certify saccades or IPA.",m_Label);
        var state=m_LiveStatus?.headsetState;var v=state?.validation;
        bool fresh=m_FollowLive && !m_DetachedReview && TrialReviewHeadsetState.Fresh(state,ReceiverConnected,UtcSeconds,m_LiveStatus?.headsetStateObservedUtc??0);
        bool pending=m_HeadsetCommand!=null && UtcSeconds-m_HeadsetCommand.createdUtc<12 && state?.commandId!=m_HeadsetCommand.id && m_LiveStatus?.controlId!=m_HeadsetCommand.id;
        GUILayout.Label(fresh?(state.eyeCaptureStatus??"Capture status unavailable"):"Connect live and wake the headset first.",m_MutedLabel);
        if(v!=null)
        {
            GUILayout.Label($"{v.participant} · {v.stage} · Step {Math.Min(v.step+1,v.count)}/{v.count} · {v.exercise} / {v.condition}",m_Heading);
            GUILayout.Label(v.prompt??"",m_Label);
            if(v.active)GUILayout.Label($"{v.remaining:F1} seconds remaining · {v.protocol}",m_MutedLabel);
            if(!string.IsNullOrEmpty(v.error))GUILayout.Label(v.error,m_Label);
            if(!string.IsNullOrEmpty(v.recordingPath))GUILayout.Label("Headset recording: "+v.recordingPath,m_MutedLabel);
        }
        bool enabled=GUI.enabled;
        GUI.enabled=enabled && fresh && !pending;
        if(v?.active==true)
        {
            GUILayout.Space(12);
            if(v.stage=="instruction" && v.exercise!="arithmetic" && Button("Begin this exercise"))SendValidation("validation_next");
            if(v.stage=="response")
            {
                GUILayout.BeginHorizontal();GUILayout.Label("Participant's spoken answer",m_Label,GUILayout.Width(240));
                m_ValidationAnswer=GUILayout.TextField(m_ValidationAnswer,m_Field,GUILayout.Width(120));
                if(Button("Record answer") && int.TryParse(m_ValidationAnswer,out int answer) && answer>=0 && answer<=999)
                {SendValidation("validation_answer",answer);m_ValidationAnswer="";}
                GUILayout.EndHorizontal();
                GUILayout.Label("Answers can also be selected with the right controller. Missing answers time out after 20 seconds.",m_MutedLabel);
            }
            if(v.stage=="rating")
            {
                GUILayout.BeginHorizontal();for(int i=1;i<=7;i++)if(Button(i.ToString(),GUILayout.Width(65)))SendValidation("validation_answer",i);GUILayout.EndHorizontal();
            }
            // Stop remains available even when another request awaits acknowledgment.
            GUI.enabled=enabled && fresh;
            if(Button("Stop validation and save incomplete recording"))SendValidation("validation_stop");
        }
        else
        {
            GUILayout.Space(16);GUILayout.Label("Participant code (no name)",m_Label);SetValidationParticipant(GUILayout.TextField(m_ValidationParticipant,m_Field));
            GUILayout.BeginHorizontal();GUILayout.Label("Participant IPD (mm)",m_Label,GUILayout.Width(180));
            m_ValidationIpd=GUILayout.TextField(m_ValidationIpd,m_Field,GUILayout.Width(90));
            GUILayout.Label("Headset lens spacing (mm)",m_Label,GUILayout.Width(240));
            m_ValidationLens=GUILayout.TextField(m_ValidationLens,m_Field,GUILayout.Width(90));GUILayout.EndHorizontal();
            m_ValidationIpdSource=GUILayout.SelectionGrid(m_ValidationIpdSource,new[]{"Not recorded","Pupillometer","Ruler","Participant report"},4);
            GUILayout.Label("Leave unknown measurements blank. Record measured IPD separately from headset lens spacing.",m_MutedLabel);
            m_ValidationMode=GUILayout.SelectionGrid(m_ValidationMode,new[]{"Full battery","Spatial dots","Saccades","Controls","IPA arithmetic","Search practice","Drift check"},3);
            m_ValidationAlignFirst=GUILayout.Toggle(m_ValidationAlignFirst,"Align gaze first, then independently check accuracy");
            if(m_ValidationAlignFirst)GUILayout.Label("Fresh three-dot alignment → three separate shape checks → wearer confirms OK → selected exercise. Skipping or failing alignment stops this attempt. Raw and corrected gaze are both saved; this correction ends with validation.",m_MutedLabel);
            m_ValidationHardFirst=GUILayout.Toggle(m_ValidationHardFirst,"Hard arithmetic first (BAAB); otherwise easy first (ABBA)");
            GUILayout.Label("Session notes: glasses, room/light conditions, fitting changes (no identifying information)",m_Label);
            m_ValidationNotes=GUILayout.TextField(m_ValidationNotes,500,m_Field);
            GUILayout.Label("Complete HTC calibration and fit the headset first. Exercise timings and thresholds are pilot settings. The gray background is a software control, not a measured luminance calibration.",m_MutedLabel);
            GUI.enabled=enabled && fresh && !pending && state.validationAvailable;
            if(Button("Start validation in headset"))SendValidation("validation_start");
            GUI.enabled=enabled;
            if(fresh && !state.validationAvailable)GUILayout.Label("Return the headset to voice/session setup or complete the study before starting validation.",m_MutedLabel);
        }
        GUI.enabled=enabled;
        if(!string.IsNullOrEmpty(m_ControlError))GUILayout.Label(m_ControlError,m_Label);
        if(m_HeadsetCommand?.action.StartsWith("validation_")==true)
            GUILayout.Label(state?.commandId==m_HeadsetCommand.id?"Headset: "+state.commandResult:pending?"Waiting for headset acknowledgment…":"Request awaiting confirmation or expired; check live status.",m_MutedLabel);
        GUILayout.Space(12);
        GUILayout.Label("Recordings sync under saved files → data → EyeMeasurements. Each validation folder contains the schedule, events, raw samples and an explicit complete/interrupted/error receipt. Close this panel without stopping to continue recording.",m_Label);
        if(Button("Open saved recordings")){Directory.CreateDirectory(m_LiveOutput);Application.OpenURL(new Uri(m_LiveOutput).AbsoluteUri);}
        GUILayout.EndScrollView();GUILayout.EndArea();
    }
}
