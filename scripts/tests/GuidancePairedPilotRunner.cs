// Offline comparative harness: production policy classes, scripted observations, simulated audio.
// This is not the Unity playback implementation and never estimates acoustic/device latency.
using System;
using System.Reflection;
using System.IO;
using System.Globalization;
class GuidancePairedPilotRunner
{
    static void Emit(string scenario, double t, string kind, GuidanceStateMachine m, GuidanceStateMachine.Input x, string extra="") =>
        Console.WriteLine(string.Join("\t",scenario,t.ToString("F3",CultureInfo.InvariantCulture),kind,m.Phase,m.Action,x.valid,x.looking,x.controllerValid&&x.controllerOnTarget,extra));
    static bool PlaybackAllowed(GuidanceStateMachine m, long revision, GuidanceAction action, GuidanceStateMachine.Input x)
    {
        var method=typeof(GuidanceStateMachine).GetMethod("AllowsPlayback");
        return method!=null ? (bool)method.Invoke(m,new object[]{revision,action,x.controllerValid,x.controllerOnTarget}) : m.Allows(revision,action);
    }
    static string Reason(GuidanceAction a)
    {
        switch(a) {
            case GuidanceAction.ConfirmGaze:return "gaze_target_vicinity_hold";
            case GuidanceAction.ConfirmPointing:return "controller_target_gaze_vicinity";
            case GuidanceAction.Arrival:return "gaze_target_plane";
            case GuidanceAction.Progress:return "turn_progress_milestone";
            case GuidanceAction.ReturnTarget:return "recent_target_departure";
            case GuidanceAction.ReturnBearing:return "target_bearing_overshoot";
            case GuidanceAction.ReturnWall:return "gaze_overshoot_return";
            default:return "stable_direction";
        }
    }
    static GuidanceStateMachine.Input Observe(string scenario,double t)
    {
        var x=new GuidanceStateMachine.Input{now=t,deltaSeconds=.05,receiptTime=t,observationSequence=(long)(t*1000),valid=true,controllerValid=true,
            inside=t>=2,looking=t>=3,controllerOnTarget=t>=4,remainingTurn=(float)Math.Max(0,180-t*90),verticalFraction=.3f};
        if(scenario=="scripted_vertical_reversal") {x.inside=true;x.looking=t>=4;x.controllerOnTarget=t>=5;x.verticalFraction=t<2?.05f:-.05f;}
        if(scenario=="controller_outside_gaze") {x.inside=true;x.looking=t<.4 || t>=2;x.controllerOnTarget=t>=.6;}
        if(scenario=="gaze_then_controller") {x.inside=true;x.looking=t>=.2;x.controllerOnTarget=t>=.4;}
        if(scenario=="controller_short_hold") {x.inside=true;x.looking=t>=.2;x.controllerOnTarget=t>=.4&&t<.9;}
        if(scenario=="leave_reacquire") {x.inside=true;x.looking=t<1.5||t>=2.5;x.controllerOnTarget=t>=.2&&t<1.5||t>=2.5;}
        if(scenario=="gaze_only") {x.inside=true;x.looking=t>=.3;x.controllerOnTarget=false;}
        if(scenario=="overlap_distractor_wins") {x.inside=true;x.looking=t>=3;x.controllerOnTarget=false;}
        if(scenario=="overshoot_return") {x.inside=t>=1&&t<2||t>=3;x.looking=t>=4;x.controllerOnTarget=t>=5;}
        if(scenario=="blink_recovery") {x.inside=true;x.looking=t>=.3;x.controllerOnTarget=t>=.4;x.valid=!(t>=.8&&t<1.5);}
        if(scenario=="controller_tracking_loss") {x.inside=true;x.looking=t>=.3;x.controllerOnTarget=true;x.controllerValid=!(t>=.8&&t<1.5);}
        if(scenario=="stale_tracking") {x.valid=!(t>=1&&t<2.5);}
        if(scenario=="rapid_direction") {x.inside=t>=3;x.looking=t>=4;x.controllerOnTarget=t>=5;}
        if(scenario.StartsWith("audio_")||scenario.StartsWith("jev_")) {x.inside=true;x.looking=t<.3||t>=3;x.controllerOnTarget=t>=.5;}
        if(scenario=="audio_hold_target") {x.inside=true;x.looking=t>=1;x.controllerOnTarget=false;}
        x.direction=x.inside?DirectionalHintPolicy.DownPhrase:DirectionalHintPolicy.RightPhrase;
        if(scenario=="scripted_vertical_reversal"&&t>=2)x.direction=DirectionalHintPolicy.UpPhrase;
        if(scenario=="overshoot_return"&&t>=2&&t<3)x.direction=DirectionalHintPolicy.LeftPhrase;
        if(scenario=="rapid_direction"&&t<3)x.direction=((int)(t/.15)%2==0)?DirectionalHintPolicy.LeftPhrase:DirectionalHintPolicy.RightPhrase;
        return x;
    }
    static void Run(string scenario)
    {
        var m=new GuidanceStateMachine();m.BeginSearch(0);
        var pending=GuidanceAction.None; long revision=0; double ready=0,ended=-100,busyUntil=scenario.StartsWith("audio_")?3:0;
        string lastAction="",lastSpokenDirection=null; double lastDirectionEnd=-100; bool paused=false; bool lastGaze=false,lastPoint=false;
        for(int tick=0;tick<=160;tick++) {
            double t=tick*.05; var x=Observe(scenario,t);
            if(scenario=="pause_recovery"&&t>=1&&t<2) {if(!paused){m.SetLifecycle(GuidancePhase.Paused,"pilot_pause",t);pending=GuidanceAction.None;paused=true;Emit(scenario,t,"pause",m,x);}continue;}
            if(paused){m.ResumeSearch(t);paused=false;Emit(scenario,t,"resume",m,x);}
            m.Step(x); Emit(scenario,t,"observation",m,x);
            if(m.Action==GuidanceAction.ConfirmGaze&&!lastGaze)Emit(scenario,t,"qualification_gaze",m,x);
            if(m.Action==GuidanceAction.ConfirmPointing&&!lastPoint)Emit(scenario,t,"qualification_controller",m,x);
            lastGaze=m.Action==GuidanceAction.ConfirmGaze;lastPoint=m.Action==GuidanceAction.ConfirmPointing;
            string state=m.Phase+"/"+m.Action;
            if(state!=lastAction){Emit(scenario,t,"state",m,x);lastAction=state;}
            if(pending!=GuidanceAction.None&&t>=ready&&t>=busyUntil) {
                var action=pending; pending=GuidanceAction.None;
                bool ack=action==GuidanceAction.ConfirmGaze||action==GuidanceAction.ConfirmPointing;
                if(!PlaybackAllowed(m,revision,action,x)){Emit(scenario,t,"discard",m,x,action.ToString());}
                else if(GuidanceSpeechPolicy.CanStart(false,t,ended,ack)) {
                    Emit(scenario,t,"simulated_start",m,x,action.ToString());
                    m.Commit(action,t,x.inside,x.controllerOnTarget,x.direction,x.remainingTurn);busyUntil=t+.4;ended=busyUntil;
                    if(action==GuidanceAction.Direction){lastSpokenDirection=x.direction;lastDirectionEnd=ended;}
                }
            }
            if(pending==GuidanceAction.None&&m.Action!=GuidanceAction.None&&t>=busyUntil) {
                bool ack=m.Action==GuidanceAction.ConfirmGaze||m.Action==GuidanceAction.ConfirmPointing;
                if(!GuidanceSpeechPolicy.CanStart(false,t,ended,ack))continue;
                if(m.Action==GuidanceAction.Direction&&x.direction==lastSpokenDirection&&t-lastDirectionEnd<GuidanceSpeechPolicy.DuplicateDirectionGap)continue;
                pending=m.Action;revision=m.Revision; double delay=GuidanceSpeechPolicy.IsPromptEvent(Reason(pending))?0:.25;
                if(delay>0&&scenario=="jev_wait")delay=GuidanceSpeechPolicy.MaximumTransmissionDeferralSeconds;
                if(delay>0&&scenario=="jev_timeout")delay=GuidanceSpeechPolicy.AdviceDeadline;
                if(delay>0&&scenario=="jev_failure")delay=.15;
                if(delay>0&&scenario=="jev_late")delay=2;
                ready=t+delay;Emit(scenario,t,"scheduled",m,x,pending+";delay="+delay.ToString("F3",CultureInfo.InvariantCulture));
            }
        }
    }
    static void RecordedSnapshots(string path)
    {
        foreach(var line in File.ReadAllLines(path)) {
            var f=line.Split('\t');
            var m=new GuidanceStateMachine();m.BeginSearch(0);
            var x=new GuidanceStateMachine.Input {valid=bool.Parse(f[1]),inside=bool.Parse(f[2]),looking=bool.Parse(f[3]),
                controllerValid=bool.Parse(f[4]),controllerOnTarget=bool.Parse(f[5]),verticalFraction=float.Parse(f[8],CultureInfo.InvariantCulture)};
            float yaw=float.Parse(f[6],CultureInfo.InvariantCulture),pitch=float.Parse(f[7],CultureInfo.InvariantCulture);
            x.direction=x.inside?DirectionalHintPolicy.WithinArea(yaw,pitch):yaw<0?DirectionalHintPolicy.LeftPhrase:DirectionalHintPolicy.RightPhrase;
            x.remainingTurn=Math.Abs(yaw);
            // Frozen observation probe, not replayed duration/qualification from the participant.
            foreach(double t in new[]{0,.3}) {x.now=t;x.deltaSeconds=.3;x.receiptTime=t;m.Step(x);Emit(f[0],t,"observation",m,x);}
        }
    }
    static void Main(string[] args) {
        foreach(var scenario in new[]{"large_turn_wall_vertical","scripted_vertical_reversal","overshoot_return","controller_outside_gaze","gaze_then_controller","controller_short_hold","leave_reacquire","gaze_only","overlap_distractor_wins","blink_recovery","controller_tracking_loss","stale_tracking","rapid_direction","pause_recovery","audio_playing","audio_hold_target","jev_wait","jev_timeout","jev_failure","jev_late"})Run(scenario);
        if(args.Length>0)RecordedSnapshots(args[0]);
    }
}
