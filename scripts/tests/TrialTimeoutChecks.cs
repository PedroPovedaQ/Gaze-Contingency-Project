using System;
public enum VoiceCondition { Generic, SelfSimilar }
class TrialTimeoutChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Main()
    {
        var c = new StudyTrialClock();
        Check(!c.DeadlineReached(999), "No deadline before reveal");
        c.Begin(10);
        Check(!c.DeadlineReached(34.999) && c.DeadlineReached(35), "Inclusive 25s deadline");
        c.Pause(14); Check(!c.DeadlineReached(1000), "Pause excludes real time");
        c.Resume(1000);
        Check(!c.DeadlineReached(1020.999) && c.DeadlineReached(1021), "Resume retains exposure");
        c.Expire(); Check(c.Elapsed(9000) == 25 && !c.DeadlineReached(9000), "Freeze timeout once");
        c.Begin(9000); Check(c.Elapsed(9000) == 0 && !c.DeadlineReached(9024), "Next trial resets");
        c.Pause(9024.9); Check(c.Elapsed(9999) < 25, "Qualified selection before limit remains a success");
        var seen = new System.Collections.Generic.HashSet<string>();
        for(int n=0;n<3;n++) {
            string line=VoicePromptText.TimeoutPhrase(n,false,false);
            Check(line.StartsWith("Time up.") && seen.Add(line), "Varied mandatory timeout announcement");
            Check(VoicePromptText.FormatForVoice(line,VoiceCondition.Generic)==VoicePromptText.FormatForVoice(line,VoiceCondition.SelfSimilar), "Same semantics in both voices");
        }
        Check(VoicePromptText.TimeoutPhrase(3,true,true)==VoicePromptText.TimeoutFinal,"Final takes priority");
        Check(VoicePromptText.TimeoutPhrase(1,false,true)==VoicePromptText.TimeoutBlockBreak,"Block break wording");
        Console.WriteLine("PASS: 25s clock, inclusive deadline, pauses, reset, wording variation and boundary/voice parity.");
    }
}
