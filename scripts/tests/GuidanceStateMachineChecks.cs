using System;
class GuidanceStateMachineChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    static GuidanceStateMachine.Input Input(double now, bool inside=false, bool looking=false, bool onTarget=false, bool valid=true) =>
        new GuidanceStateMachine.Input { now=now, deltaSeconds=.1, valid=valid, inside=inside, looking=looking,
            controllerValid=true, controllerOnTarget=onTarget, direction=inside?DirectionalHintPolicy.DownPhrase:DirectionalHintPolicy.RightPhrase,
            verticalFraction=.3f, remainingTurn=160, observationSequence=(long)(now*1000), receiptTime=now };
    static void CheckAdditionalTransitions()
    {
        var m = new GuidanceStateMachine();
        m.SetLifecycle(GuidancePhase.Opening, "readiness", 0);
        Check(m.Action == GuidanceAction.None && m.Phase == GuidancePhase.Opening, "Opening is not active search");
        m.BeginSearch(1); m.Step(Input(1)); m.Step(Input(1.3));
        long ticket = m.Revision;
        Check(m.Allows(ticket, GuidanceAction.Direction), "Current direction ticket eligible");
        m.Step(Input(1.4));
        Check(m.Allows(ticket, GuidanceAction.Direction), "A new sample in the same phase does not invalidate a request");
        m.Step(Input(1.5, true)); m.Step(Input(1.8, true));
        Check(!m.Allows(ticket, GuidanceAction.Direction), "Old direction answer cannot survive arrival");
        var snapshot = m.Snapshot(1.8, 12, true, true, "distractor", "target", "other", "target");
        Check(snapshot.phase == "Refining" && snapshot.allowedAction == "Arrival" && snapshot.gazeHitObjectId == "distractor" &&
            snapshot.vicinityWinnerObjectId == "target" && snapshot.controllerHitObjectId == "other", "Direct ray, vicinity and controller IDs stay distinct");
        var unavailable = m.Snapshot(1.9, 13, false, false, "old", "old", "old", "target");
        Check(unavailable.gazeHitObjectId == null && unavailable.vicinityWinnerObjectId == null && unavailable.controllerHitObjectId == null,
            "Unavailable observations never retain old hit markers");
        Check(snapshot.gazeHitObjectId == "distractor", "Creating a later snapshot does not mutate the earlier record");
        m.Commit(GuidanceAction.Arrival, 1.8, true);
        m.Step(Input(2, true, true)); m.Step(Input(2.11, true, true));
        m.Commit(GuidanceAction.ConfirmGaze, 2.11, true);
        for (int i=1;i<=31;i++) m.Step(Input(2.11+i*.1, true, true));
        Check(m.Action == GuidanceAction.SelectionReminder, "Three seconds of sustained acknowledged target gaze offers selection reminder");
        m.Commit(GuidanceAction.SelectionReminder, 5.21, true);
        m.SetLifecycle(GuidancePhase.Paused, "pause", 5.3); m.ResumeSearch(6);
        Check(m.TargetAcknowledged, "Resume preserves already acknowledged objective");
        m.Step(Input(6, true, true)); m.Step(Input(6.11, true, true));
        m.Commit(GuidanceAction.ConfirmGaze, 6.11, true);
        for (int i=1;i<=31;i++) m.Step(Input(6.11+i*.1, true, true));
        Check(m.Action != GuidanceAction.SelectionReminder, "Resume does not rearm a consumed once-per-objective reminder");
        m.BeginSearch(10); Check(!m.TargetAcknowledged, "New objective clears old acknowledgment");
        m.Step(Input(10));m.Step(Input(10.3));
        m.Commit(GuidanceAction.Direction, 10.3, false, false, DirectionalHintPolicy.RightPhrase, 180);
        var progress = Input(10.4); progress.remainingTurn=130; m.Step(progress);
        Check(m.Action == GuidanceAction.Progress, "New turn milestone outranks repeated direction");
        m.Commit(GuidanceAction.Progress,10.4,false,false,DirectionalHintPolicy.RightPhrase,130);
        progress.now=10.5;m.Step(progress);Check(m.Action == GuidanceAction.Direction, "Consumed progress does not loop");
        var turn = new GuidanceTurnState(); turn.Prime(DirectionalHintPolicy.RightPhrase);
        Check(turn.Peek(-30,false)==DirectionalHintPolicy.LeftPhrase, "Read-only check follows current non-seam bearing");
        Check(turn.Peek(-179,false)==DirectionalHintPolicy.RightPhrase, "Read-only check cannot mutate locked rear route");
        turn.Peek(0,true);
        Check(turn.Peek(-179,false)==DirectionalHintPolicy.RightPhrase, "Read-only within-wall check cannot reset route memory");
    }
    static void Main()
    {
        var m=new GuidanceStateMachine(); m.BeginSearch(0);
        m.Step(Input(0)); m.Step(Input(.3));
        Check(m.Phase==GuidancePhase.Turning && m.Action==GuidanceAction.Direction,"Stable outside-wall state owns direction eligibility");
        long old=m.Revision;
        m.Step(Input(.4,true)); Check(m.Phase==GuidancePhase.Refining && m.Action==GuidanceAction.None,"Wall entry changes phase before arrival debounce completes");
        Check(m.Revision!=old,"Leaving turning invalidates old phase tickets");
        m.Step(Input(.7,true)); Check(m.Action==GuidanceAction.Arrival,"Stable arrival outranks refinement");
        m.Commit(GuidanceAction.Arrival,.7,true);m.Step(Input(.8,true));
        Check(m.Action==GuidanceAction.Direction,"Consumed arrival is not repeated during vertical search");
        m.Step(Input(.9,true,true));m.Step(Input(1.01,true,true));
        Check(m.Phase==GuidancePhase.Acquired && m.Action==GuidanceAction.ConfirmGaze,"A tenth of a second qualifies confirmation");
        m.Step(Input(1.2,true,true));Check(m.Action==GuidanceAction.ConfirmGaze,"Busy or waiting speech never consumes confirmation");
        m.Commit(GuidanceAction.ConfirmGaze,1.2,true);m.Step(Input(1.3,true,true));Check(m.Action==GuidanceAction.None,"Continued gaze cannot repeatedly confirm");
        m.Step(Input(1.4,true,true,true,false));Check(m.Phase==GuidancePhase.Suspended && m.Action==GuidanceAction.None,"Invalid gaze suspends decisions even with controller hit");
        m.Step(Input(2.5,true,true,true));m.Step(Input(2.61,true,true,true));
        Check(m.Phase==GuidancePhase.ReadyToSelect && m.PointingStable,"Controller hold survives a blink");
        Check(m.Action!=GuidanceAction.ReturnTarget && m.Action!=GuidanceAction.ReturnWall,"Blink never fabricates departure");
        m.Commit(GuidanceAction.ConfirmPointing,2.61,true);
        m.Step(Input(2.7,true,false));m.Step(Input(2.86,true,false));m.Step(Input(2.9,true,true));m.Step(Input(3.01,true,true));
        Check(m.Action==GuidanceAction.ConfirmGaze,"Valid departure and reacquisition rearms one confirmation");
        old=m.Revision;m.SetLifecycle(GuidancePhase.Paused,"researcher_pause",3.1);
        Check(m.Phase==GuidancePhase.Paused && m.Action==GuidanceAction.None && m.Revision!=old,"Pause invalidates pending work");
        m.ResumeSearch(4);m.Step(Input(4));m.Step(Input(4.3));Check(m.Phase==GuidancePhase.Turning,"Resume can establish current direction");
        m.SetLifecycle(GuidancePhase.Completed,"correct_selection",5);Check(m.Phase==GuidancePhase.Completed,"Success has explicit state");
        m.SetLifecycle(GuidancePhase.Stopped,"technical_stop",6);Check(m.Phase==GuidancePhase.Stopped,"Technical stop is not success");
        CheckAdditionalTransitions();
        CheckReturnActionPriority();
        Console.WriteLine("PASS centralized guidance: arrival, confirmation, consumption, blink, reacquisition, pause/resume and lifecycle tickets");
    }

    static void CheckReturnActionPriority()
    {
        var m = new GuidanceStateMachine();
        m.BeginSearch(0);
        m.Step(Input(0,true,true));m.Step(Input(.11,true,true));
        m.Commit(GuidanceAction.ConfirmGaze,.11,true);
        m.Step(Input(.2,true));m.Step(Input(.51,true));
        Check(m.Action==GuidanceAction.ReturnTarget,"Sustained target departure outranks ordinary refinement");
        m.Step(Input(.6,true));
        Check(m.Action==GuidanceAction.ReturnTarget,"Unplayed target-return cue stays eligible");
        m.Commit(GuidanceAction.ReturnTarget,.6,true);
        m.Step(Input(.7,true));
        Check(m.Action!=GuidanceAction.ReturnTarget,"Only playback consumes target-return cue");

        m.BeginSearch(2);
        m.Step(Input(2,true));m.Step(Input(2.3,true));
        m.Commit(GuidanceAction.Arrival,2.3,true);
        m.Step(Input(2.4));m.Step(Input(2.71));
        Check(m.Action==GuidanceAction.ReturnWall,"Sustained wall departure outranks ordinary turning");
        m.Commit(GuidanceAction.ReturnWall,2.71,false);
        m.Step(Input(2.8));
        Check(m.Action!=GuidanceAction.ReturnWall,"Consumed wall-return does not repeat");

        m.BeginSearch(4);
        for(int i=0;i<=20;i++)
        {
            double time=4+i*.04;
            var sample=Input(time,true);
            sample.history=new GazeTargetHistory.Sample{
                time=time,inputTime=time,observationSequence=i+1,
                deviceId=1,eyeRecordingId="test",yawError=i<3?8:i<5?0:-8,pitchError=0};
            m.Step(sample);
        }
        Check(m.Action==GuidanceAction.ReturnBearing,"Measured bearing passage outranks unspoken wall arrival");
        m.Commit(GuidanceAction.ReturnBearing,4.8,true);
        m.Step(Input(4.9,true));
        Check(m.Action!=GuidanceAction.ReturnBearing,"Played bearing return clears pending trajectory");
        m.SetLifecycle(GuidancePhase.Paused,"test_pause",5);
        m.ResumeSearch(6);m.Step(Input(6));m.Step(Input(6.3));
        Check(m.Action==GuidanceAction.Direction,"Resume does not reuse pre-pause return history");
    }
}
