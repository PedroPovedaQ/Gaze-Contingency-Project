using System;
class GazeQualityChecks
{
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); }
    static int Main()
    {
        Check(GazeQualityPolicy.IsFresh(true, 10, 10.05, .1), "fresh tracked sample");
        Check(!GazeQualityPolicy.IsFresh(false, 10, 10.05, .1), "invalid sample");
        Check(!GazeQualityPolicy.IsFresh(true, 10, 10.2, .1), "stale sample");
        Check(!GazeQualityPolicy.IsFresh(true, double.NaN, 10, .1), "unknown sample time");
        Check(!GazeQualityPolicy.IsFresh(true, 11, 10, .1), "future timestamp");
        Check(GazeQualityPolicy.Vertical(0, 15, 3) == "Look up.", "target above gaze");
        Check(GazeQualityPolicy.Vertical(15, 0, 3) == "Look down.", "target below gaze");
        Check(GazeQualityPolicy.Vertical(15, 16, 3) == "Right there.", "vertical alignment");
        var gate = new GazeZoneEntryGate();
        gate.Update(true,true,0); Check(gate.Update(true,true,.11),"stable entry");gate.MarkSpoken(.11);
        Check(!gate.Update(true,true,8),"vertical scanning stays in same sector");
        gate.Update(false,false,9);Check(!gate.Update(true,true,10),"blink cannot rearm entry");
        var pointing = new GazeZoneEntryGate(0, 1.5);
        pointing.Update(true,true,0);
        Check(!pointing.Update(true,true,1.49),"brief controller hit cannot announce target");
        Check(pointing.Update(true,true,1.51),"continuous 1.5 second target hold qualifies");pointing.MarkSpoken(1.51);
        Check(!pointing.Update(true,true,3),"holding the target does not repeat confirmation");
        pointing.Update(false,false,3.1);pointing.Update(true,true,3.2);
        Check(!pointing.Update(true,true,4.8),"blink does not rearm spoken confirmation");
        pointing.Update(true,false,5);pointing.Update(true,false,5.51);pointing.Update(true,true,6);
        Check(!pointing.Update(true,true,7.49),"reacquisition needs a new full hold");
        Check(pointing.Update(true,true,7.51),"genuine reacquisition can confirm after full hold");
        pointing.Reset();pointing.Update(true,true,0);pointing.Update(false,false,1.4);pointing.Update(true,true,1.5);
        Check(!pointing.Update(true,true,2.9),"tracking gap resets incomplete hold");
        Check(pointing.Update(true,true,3.01),"fresh full hold after tracking recovery");
        Check(DirectionalHintPolicy.PointingPhrase=="You're on it!","requested short confirmation");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",40,60,0,true)=="Keep going.","progressing same direction gets continuation");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",65,60,0,true)=="Turn to your right.","moving away retains explicit direction");
        Check(DirectionalHintPolicy.FollowUp("Look left.","Look right.",40,60,0,true)=="Turn to your left.","direction change is explicit");
        Check(DirectionalHintPolicy.WithinArea(0,2.8f,"Look up.")=="Look up.","near-threshold jitter retains direction");
        Check(DirectionalHintPolicy.WithinArea(0,1,"Look up.")=="Right there.","alignment releases upward guidance");
        Check(DirectionalHintPolicy.WithinArea(0,3.5f,"Right there.")=="Right there.","small departure does not restart upward speech");
        Check(DirectionalHintPolicy.WithinArea(0,6,"Right there.")=="Look up.","meaningful departure restores direction");
        Check(!DirectionalHintPolicy.RequiresInterruption("Look up.","Right there."),"arrival at target lets short cue finish");
        Check(DirectionalHintPolicy.RequiresInterruption("Look up.","Look down."),"reversal still interrupts");
        Check(!DirectionalHintPolicy.RequiresInterruption("Look right.","Look right."),"same direction never interrupts itself");
        var back=new GazeReturnCueGate();back.Update(true,true,0);back.Update(true,true,.2);
        back.Update(false,false,.3);Check(!back.Update(true,false,.7),"loss is not overshoot");
        GazeTargetHistoryChecks.Run();
        Console.WriteLine("PASS gaze freshness, vertical direction, same-wall scan and tracking gap gates");return 0;
    }
}
