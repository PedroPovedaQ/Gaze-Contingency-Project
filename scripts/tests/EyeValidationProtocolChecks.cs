using System;
using System.Linq;
static class EyeValidationProtocolChecks
{
    static void Check(bool ok,string why) { if(!ok)throw new Exception(why); }
    static void Main()
    {
        var a=EyeValidationProtocol.Build("full",42,false);
        var b=EyeValidationProtocol.Build("full",42,true);
        foreach(string exercise in new[]{"spatial","fixation","saccades","blinks","head_turn","pursuit","luminance","gaze_angle","arithmetic","search","drift"})
            Check(a.Any(s=>s.exercise==exercise),"Missing exercise "+exercise);
        Check(a.Where(s=>s.exercise=="arithmetic").First().condition=="easy","Easy first");
        Check(b.Where(s=>s.exercise=="arithmetic").First().condition=="hard","Hard first");
        Check(a.Count(s=>s.exercise=="arithmetic"&&s.condition=="easy")==6,"Six easy problems");
        Check(a.Count(s=>s.exercise=="arithmetic"&&s.condition=="hard")==6,"Six hard problems");
        Check(a.All(s=>s.seconds>0&&s.distance>0),"Positive scheduled durations and distances");
        Check(EyeValidationProtocol.Build("saccades",42,false).All(s=>s.exercise=="fixation"||s.exercise=="saccades"),"Subset isolation");
        bool rejected=false;try{EyeValidationProtocol.Build("unknown",1,false);}catch(ArgumentException){rejected=true;}
        Check(rejected,"Unknown exercise rejected");
        var p=new EyeValidationProgress(2);
        Check(!p.Next(0,0),"Cannot start before minimum instruction exposure");
        Check(p.Next(0,2)&&p.stage=="running","Start from instruction");
        Check(!p.Next(0,3),"Delayed instruction command cannot skip running stimulus");
        Check(p.FinishStimulus(12,true)&&p.stage=="response","Wait for response");
        Check(!p.Answer(0,13),"Stale response rejected");
        Check(p.Answer(p.revision,13)&&p.index==1,"Exactly one response advances");
        p.Abort();Check(!p.Next(p.revision,30)&&p.stage=="aborted","Abort is terminal");
        Console.WriteLine("PASS: validation schedule, counterbalance, stale commands and terminal state");
    }
}
