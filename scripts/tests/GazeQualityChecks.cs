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
        var back=new GazeReturnCueGate();back.Update(true,true,0);back.Update(true,true,.2);
        back.Update(false,false,.3);Check(!back.Update(true,false,.7),"loss is not overshoot");
        Console.WriteLine("PASS gaze freshness, vertical direction, same-wall scan and tracking gap gates");return 0;
    }
}
