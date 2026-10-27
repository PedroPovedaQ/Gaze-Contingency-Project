using System;
class ActivationCheckProgressChecks
{
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static void Hold(ActivationCheckProgress state, int target, double start)
    { for (int i = 0; i < 10; i++) state.Observe(true, target, start + i * .05); }
    static void Main()
    {
        var state = new ActivationCheckProgress();
        Check(!state.Ready && state.Select(0) == "gaze_not_ready" && state.Step == 0, "Trigger alone cannot advance");
        Hold(state, 1, 0);
        Check(!state.Ready && state.Step == 0, "Wrong gaze cannot qualify");
        Hold(state, 0, 1);
        Check(state.Ready && state.Select(1) == "wrong_object" && state.Step == 0, "Wrong controller target cannot advance");
        state.Observe(false, 0, 1.46);
        Check(!state.Ready && state.Select(0) == "gaze_not_ready", "Tracking loss clears gaze qualification");
        Hold(state, 0, 2); state.Observe(true, 1, 2.46);
        Check(!state.Ready, "Looking away clears gaze qualification");
        Hold(state, 0, 3); state.Observe(true, 0, 4);
        Check(!state.Ready, "Stale frame gaps cannot preserve qualification");
        Hold(state, 0, 5); state.Observe(true, 0, 4);
        Check(!state.Ready, "Reversed time resets qualification");
        Hold(state, 0, 6); state.Observe(true, 0, double.NaN);
        Check(!state.Ready, "Nonfinite time rejected");
        Hold(state, 0, 7);
        Check(state.Select(0) == "correct" && state.Step == 1 && !state.Ready, "Fresh gaze plus intended selection advances once");
        Check(state.Select(1) == "gaze_not_ready", "Next object needs a new gaze qualification");
        Hold(state, 1, 8); Check(state.Select(1) == "correct", "Second target qualifies independently");
        Hold(state, 2, 9); Check(state.Select(2) == "correct" && state.Finished, "Three targets complete");
        Check(state.Select(2) == "already_complete" && state.Step == 3, "Completion cannot repeat");
        Console.WriteLine("PASS: activation gaze/trigger order, wrong targets, gaps, tracking loss, clock safety and three-object completion");
    }
}
