/// <summary>Pilot conversational pacing based on phrase meaning and actual playback, not request age.</summary>
public static class GuidanceSpeechPolicy
{
    public const double FadeSeconds = .150;
    public const double AdviceDeadline = .400;
    public const double InterruptionCooldown = 2.5;
    // Protect short phrases and their endings. Jev cannot override these local bounds.
    public static bool CanConsiderInterruption(bool acknowledgment, double elapsed, double remaining,
        double now, double lastInterruption) => !acknowledgment && elapsed >= .65 && remaining >= 1.0 &&
        now - lastInterruption >= InterruptionCooldown;

    // These events have independent, stable geometry/history. Routine nudges never cut speech.
    public static bool IsCorrectionEvent(string reason) => reason == "gaze_target_plane" ||
        reason == "stable_wall_entry" || reason == "target_bearing_overshoot" ||
        reason == "gaze_overshoot_return" || reason == "recent_target_departure" ||
        reason == "gaze_target_vicinity_hold" || reason == "controller_target_gaze_vicinity";

    public static bool IsRedundantArrival(string reason, string speechStartStage) =>
        (reason == "gaze_target_plane" || reason == "stable_wall_entry") && speechStartStage == "within_target_wall";

    public const double OrdinaryGap = .55;
    public const double AcknowledgmentGap = 1.0;
    public const double TrackingGrace = .25;

    public static bool IsAcknowledgment(string phrase)
    {
        if (phrase == DirectionalHintPolicy.PointingPhrase) return true;
        for (int i = 0; i < 4; i++)
            if (phrase == DirectionalHintPolicy.Variant(DirectionalHintPolicy.LookingPhrase, i)) return true;
        return false;
    }

    public static bool GapElapsed(double now, double ended, bool acknowledgment) =>
        now - ended >= (acknowledgment ? AcknowledgmentGap : OrdinaryGap);

    public static bool CanStart(bool busy, double now, double ended, bool acknowledgment) =>
        !busy && GapElapsed(now, ended, acknowledgment);
}
