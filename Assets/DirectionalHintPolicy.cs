using System;

/// <summary>Horizontal direction from gaze to target; Unity +X is right and +Z is forward.</summary>
public static class DirectionalHintPolicy
{
    public const string LeftPhrase = "Look left.";
    public const string RightPhrase = "Look right.";
    public const string AreaPhrase = "You're in the correct area. Keep looking.";
    public const string Version = "gaze-directional-v2-variants";

    static readonly string[] k_Left = { LeftPhrase, "Look to your left.", "Try looking left.", "Search to your left." };
    static readonly string[] k_Right = { RightPhrase, "Look to your right.", "Try looking right.", "Search to your right." };

    // Separate semantic direction from wording, so changing phrasing never looks
    // like a gaze reversal and accidentally interrupts the current hint.
    public static string Variant(string direction, int index)
    {
        if (direction != LeftPhrase && direction != RightPhrase) return direction;
        int slot = ((index % 4) + 4) % 4;
        return (direction == LeftPhrase ? k_Left : k_Right)[slot];
    }

    public static string[] AllPhrases() => new[]
    {
        LeftPhrase, RightPhrase, "Look to your left.", "Look to your right.",
        "Try looking left.", "Try looking right.", "Search to your left.", "Search to your right.", AreaPhrase
    };

    public static string Direction(float gazeX, float gazeZ, float targetX, float targetZ)
    {
        double gazeLength = gazeX * gazeX + gazeZ * gazeZ;
        double targetLength = targetX * targetX + targetZ * targetZ;
        if (double.IsNaN(gazeLength) || double.IsInfinity(gazeLength) || gazeLength < 0.0001 ||
            double.IsNaN(targetLength) || double.IsInfinity(targetLength) || targetLength < 0.0001) return null;
        double cross = gazeZ * targetX - gazeX * targetZ;
        double dot = gazeX * targetX + gazeZ * targetZ;
        double degrees = Math.Atan2(cross, dot) * 180 / Math.PI;
        // Aligned horizontally but outside the wall's vertical limits: avoid a false area cue.
        if (Math.Abs(degrees) < 1) return null;
        // Either turn is equal behind the user; consistently choose right to prevent jitter.
        if (Math.Abs(degrees) > 175) return RightPhrase;
        return degrees > 0 ? RightPhrase : LeftPhrase;
    }
}
