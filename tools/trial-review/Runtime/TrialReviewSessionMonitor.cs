using System;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    RenderTexture m_MonitorTexture;
    bool SessionMonitorVisible => m_LiveStatus?.headsetState != null;
    float SessionMonitorHeight => SessionMonitorVisible ? 140 : 0;

    // Display-only: no commands, timers or headset state are changed here.
    void DrawSessionMonitor(float width)
    {
        if(!SessionMonitorVisible)return;
        var state=m_LiveStatus.headsetState;
        double age=Math.Max(0,UtcSeconds-m_LiveStatus.headsetStateObservedUtc);
        bool fresh=TrialReviewHeadsetState.Fresh(state,ReceiverConnected,UtcSeconds,m_LiveStatus.headsetStateObservedUtc);
        string warning=MonitorWarning(state,fresh);
        double advance=fresh?age:0;
        Panel(new Rect(8,78,width-16,132));
        var color=string.IsNullOrEmpty(warning)?m_Ok:m_Warn;
        StatusChip(new Rect(20,86,230,26),fresh?"LIVE LINK · "+age.ToString("0.0")+"s ago":"UPDATES STOPPED · "+age.ToString("0")+"s",color);
        GUI.Label(new Rect(264,86,width-494,28),(state.participantCode??"")+" · "+
            (string.IsNullOrEmpty(state.screen)?state.message??"Waiting for headset status":state.screen),m_Heading);
        GUI.Label(new Rect(20,119,width-240,26),
            $"On screen {MonitorTime(state.screenElapsedSeconds+advance)}   ·   Session {MonitorTime(state.sessionElapsedSeconds+advance)}   ·   {state.trialsCompleted} / {state.trialTotal} trials completed   ·   {state.voiceBlock}",m_Label);
        GUI.Label(new Rect(20,148,width-240,23),(fresh?"Gaze: ":"Last gaze: ")+state.gazeStatus+"   ·   "+
            (string.IsNullOrEmpty(warning)?"Headset connected":warning),new GUIStyle(m_Label){normal={textColor=color}});
        string faults=string.Join("   ",Array.FindAll(new[]{state.audioFault,state.modelFault},x=>!string.IsNullOrEmpty(x)));
        GUI.Label(new Rect(20,175,width-240,25),string.IsNullOrEmpty(faults)?"No reported audio or model fault":faults,m_MutedLabel);
        var preview=new Rect(width-204,86,184,103);
        if(m_Renderer!=null && m_ViewLive && fresh && m_LiveSignalActive && Event.current.type==EventType.Repaint)
        {
            bool head=m_Renderer.RecordedHead,overhead=m_Renderer.Overhead;
            var priorViewport=m_Viewport;
            var camera=m_Renderer.Camera;
            var target=camera.targetTexture;
            var active=RenderTexture.active;
            float aspect=camera.aspect;
            try
            {
                m_Renderer.RecordedHead=true;m_Renderer.Overhead=false;
                m_Renderer.Prepare(m_Timeline,m_Time,maxSequence:m_Sequence);
                // Keep the main view's queued GUI texture alive and restore the GUI render target.
                if(m_MonitorTexture==null)m_MonitorTexture=new RenderTexture(184,103,24);
                camera.targetTexture=m_MonitorTexture;camera.aspect=184f/103;
                camera.Render();RenderTexture.active=active;
                GUI.DrawTexture(preview,m_MonitorTexture,ScaleMode.ScaleToFit,false);
                if(m_Actors?.GazeAvailable==true && m_Environment!=null && m_Renderer.UsingRecordedHead &&
                    m_Environment.TryProjectGaze(new Ray(m_Actors.GazeRay.origin,m_Actors.GazeRay.direction),out var point))
                {m_Viewport=preview;DrawGazeDot(point);}
            }
            finally {RenderTexture.active=active;camera.targetTexture=target;camera.aspect=aspect;m_Viewport=priorViewport;m_Renderer.RecordedHead=head;m_Renderer.Overhead=overhead;m_Renderer.Prepare(m_Timeline,m_Time,maxSequence:m_Sequence);}
        }
        else GUI.Label(preview,"First-person reconstruction\n"+(fresh?"Available when trial poses arrive":"Paused · link stale"),m_MutedLabel);
    }
    internal static string MonitorWarning(TrialReviewHeadsetState state,bool fresh) => !fresh ? "No fresh updates · check connection or headset sleep" :
        state.appPaused ? "Headset app paused / sleeping" : !state.appFocused ? "Headset app out of focus" :
        state.presenceKnown && !state.userPresent ? "Headset removed" : !state.presenceKnown ? "Wear detection unavailable" : "";
    static string MonitorTime(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss");
}
