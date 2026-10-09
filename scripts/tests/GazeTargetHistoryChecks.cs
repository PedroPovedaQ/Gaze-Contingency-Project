using System;
static class GazeTargetHistoryChecks
{
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    static GazeTargetHistory.Sample S(double t, float yaw, float pitch = 0, string file = "eyes-a") =>
        new GazeTargetHistory.Sample { time = t, inputTime = t, observationSequence = (long)(t * 1000),
            eyeRecordingId = file, yawError = yaw, pitchError = pitch };
    static void Observe(GazeTargetHistory h, double t, float yaw, float pitch = 0) => h.Observe(true, S(t, yaw, pitch));
    static double Pass(GazeTargetHistory h, bool horizontal, float sign = 1, double start = 1)
    {
        float[] path = { 8, 5, 2, .5f, -1, -3.5f, -4, -4.5f, -5, -5.5f, -6 };
        for (int i = 0; i < path.Length; i++)
            Observe(h, start + i * .05, horizontal ? sign * path[i] : 0, horizontal ? 0 : sign * path[i]);
        return start + (path.Length - 1) * .05;
    }
    public static void Run()
    {
        foreach (bool horizontal in new[] { true, false })
        foreach (float sign in new[] { -1f, 1f })
        {
            var h = new GazeTargetHistory(); double now = Pass(h, horizontal, sign);
            string expected = horizontal ? (sign > 0 ? DirectionalHintPolicy.LeftPhrase : DirectionalHintPolicy.RightPhrase) :
                (sign > 0 ? DirectionalHintPolicy.DownPhrase : DirectionalHintPolicy.UpPhrase);
            Check(h.PendingDirection(now) == expected, "real same-wall passage returns toward target on either axis");
            Check(h.Snapshot()[0].eyeRecordingId == "eyes-a" && h.Snapshot()[0].observationSequence == 1000,
                "history preserves exact raw recording and observation identity");
            Check(h.PendingDirection(now + .11) == null, "stale history cannot request a correction");
            h.MarkSpoken(now); double again = Pass(h, horizontal, sign, now + .05);
            Check(h.PendingDirection(again) == null, "one crossing cannot spam corrections through cooldown");
            h.Reset(); now = Pass(h, horizontal, sign, 5);
            Check(h.PendingDirection(now) == expected, "new trial resets cooldown/evidence");
        }
        var jitter = new GazeTargetHistory();
        for (int i = 0; i < 200; i++) Observe(jitter, 1 + i * .02, i % 2 == 0 ? .8f : -.8f);
        Check(jitter.PendingDirection(4.98) == null, "near-target jitter is not overshoot");
        var wrongHeight = new GazeTargetHistory();
        for (int i = 0; i < 20; i++) Observe(wrongHeight, 1 + i * .05, 8 - i, 30);
        Check(wrongHeight.PendingDirection(1.95) == null, "crossing the wall bearing far above target is not object passage");
        var wrap = new GazeTargetHistory();
        for (int i = 0; i < 30; i++) Observe(wrap, 1 + i * .02, i < 10 ? 179 : -179);
        Check(wrap.PendingDirection(1.58) == null, "behind-user wrap is not target crossing");
        var gap = new GazeTargetHistory(); Observe(gap, 1, 8); Observe(gap, 1.05, .5f);
        gap.Observe(false, S(1.06, 0));
        for (int i = 0; i < 15; i++) Observe(gap, 1.1 + i * .02, -4);
        Check(gap.PendingDirection(1.38) == null, "blink cannot establish passage");
        var missing = new GazeTargetHistory(); Observe(missing, 1, 8); Observe(missing, 1.05, .5f);
        for (int i = 0; i < 15; i++) Observe(missing, 1.5 + i * .02, -4);
        Check(missing.PendingDirection(1.78) == null, "unobserved time breaks passage evidence");
        var duplicates = new GazeTargetHistory(); Observe(duplicates, 1, 8);
        duplicates.Observe(true, new GazeTargetHistory.Sample {time=1.01,inputTime=1,yawError=0,eyeRecordingId="eyes-a"});
        Check(duplicates.Count == 1, "heartbeat duplicate does not create a near-target observation");
        var switched = new GazeTargetHistory(); Observe(switched, 1, 8); Observe(switched, 1.05, 0);
        switched.Observe(true, S(1.1,-4,0,"eyes-b"));
        Check(switched.Count == 1, "recording changes break evidence");
        var device = new GazeTargetHistory(); Observe(device, 1, 8); Observe(device, 1.05, 0);
        var differentDevice = S(1.1, -4); differentDevice.deviceId = 2; device.Observe(true, differentDevice);
        Check(device.Count == 1, "device change breaks crossing evidence");
        var reacquired = new GazeTargetHistory(); double finish = Pass(reacquired, true);
        Check(reacquired.PendingDirection(finish) != null, "return cue initially available");
        Observe(reacquired, finish + .05, 0);
        Check(reacquired.PendingDirection(finish + .05) == null, "near-target reacquisition cancels pending return");
        var noNear = new GazeTargetHistory();
        for (int i = 0; i < 30; i++) Observe(noNear, 1 + i * .02, i < 10 ? 8 : -8);
        Check(noNear.PendingDirection(1.58) == null, "unobserved jump across target does not fabricate passage");
        var bounded = new GazeTargetHistory();
        for (int i = 0; i < 5000; i++) Observe(bounded, 1 + i * .001, 30);
        Check(bounded.Count <= GazeTargetHistory.Capacity && bounded.Snapshot()[0].time >= 2.999, "history bounded by age and count");
        Check(!GazeTargetHistory.StillBeyond(DirectionalHintPolicy.LeftPhrase, 0, 0), "return to target cancels correction");
        Check(DirectionalHintPolicy.Opening(DirectionalHintPolicy.LeftPhrase) == "The object is to the left.", "requested left opening");
        Check(DirectionalHintPolicy.Opening(DirectionalHintPolicy.RightPhrase) == "The object is to the right.", "requested right opening");
        Check(DirectionalHintPolicy.Opening(DirectionalHintPolicy.UpPhrase).Contains("above"), "already aligned yaw never invents left/right");
        Console.WriteLine("PASS target-bearing history: horizontal/vertical passage, jitter, wrap, gaps, duplicates, cooldown and bounded evidence");
    }
}
