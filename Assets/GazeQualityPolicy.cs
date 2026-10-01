using System;

/// <summary>Quality decisions use delivery timestamps, never a guessed sensor rate.</summary>
public static class GazeQualityPolicy
{
    public static bool IsFresh(bool valid, double delivered, double now, double maxAge) => valid &&
        !double.IsNaN(delivered) && !double.IsInfinity(delivered) && delivered > 0 &&
        now >= delivered && now - delivered <= maxAge;

    // Pilot tolerance, not a validated fixation/object-hit threshold.
    public static string Vertical(float gazeElevation, float targetElevation, float tolerance) =>
        targetElevation - gazeElevation > tolerance ? "Look up." :
        gazeElevation - targetElevation > tolerance ? "Look down." : "Right there.";
}
