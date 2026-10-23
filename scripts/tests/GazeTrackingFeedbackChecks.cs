using System;

static class GazeTrackingFeedbackChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    public static void Run()
    {
        var state = new GazeTrackingFeedbackState();
        state.Step(true, false, 10);
        state.Step(true, false, 10.2);
        Check(!state.Visible, "Ordinary blink must not show a tracking warning");
        state.Step(true, true, 10.3);
        state.Step(true, false, 10.4);
        state.Step(true, false, 11.39);
        Check(!state.Visible, "Separate blinks must not accumulate into a warning");
        state.Step(true, false, 11.4);
        Check(state.Visible, "Sustained missing gaze shows a warning");
        state.Step(true, true, 12);
        state.Step(true, true, 12.2);
        Check(state.Visible, "One brief valid interval cannot hide the warning");
        state.Step(true, false, 12.3);
        state.Step(true, true, 12.4);
        state.Step(true, true, 12.76);
        Check(!state.Visible, "Stable recovery clears the warning automatically");
        state.Step(true, false, 13); state.Step(true, false, 14);
        state.Step(false, false, 15);
        Check(!state.Visible, "Application focus loss hides warning and clears timers");
        state.Step(true, false, 100);
        Check(!state.Visible, "Background time cannot trigger a warning on resume");
        state.Step(true, false, 101);
        Check(state.Visible, "Missing stream/device can still trigger warning after resume");
        state.Reset();
        Check(!state.Visible, "Disable clears feedback");
        Check(GazeTrackingFeedbackState.Reason(true,true,true,false,true,true).Contains("left eye"), "Reproduced left-eye failure is specific");
        Check(GazeTrackingFeedbackState.Reason(true,true,true,true,false,true).Contains("right eye"), "Right-eye failure is specific");
        Check(GazeTrackingFeedbackState.Reason(true,true,true,false,false,true).Contains("Neither"), "Both-eye loss is specific");
        Check(GazeTrackingFeedbackState.Reason(true,false,true,false,true,true).Contains("updates"), "Stale data must not diagnose a particular eye");
        Check(GazeTrackingFeedbackState.Reason(true,true,false,false,true,true).Contains("usable"), "Old native eye flags must not diagnose a particular eye");
        Check(GazeTrackingFeedbackState.Reason(false,false,false,false,false,false).Contains("data is unavailable"), "Missing stream/device has an actionable status");
        Check(GazeTrackingFeedbackState.Reason(true,true,true,true,true,false).Contains("position"), "Missing head tracking is identified");
        Console.WriteLine("PASS: gaze warning debounce, stable recovery, resume and eye-specific reasons");
    }
}
