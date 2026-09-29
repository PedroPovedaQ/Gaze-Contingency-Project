using System;

/// <summary>Horizontal direction from gaze to target; Unity +X is right and +Z is forward.</summary>
public static class DirectionalHintPolicy
{
    public const string LeftPhrase = "Look left.";
    public const string RightPhrase = "Look right.";
    public const string FarLeftPhrase = "Look way left.";
    public const string FarRightPhrase = "Look way right.";
    public const string ReturnPhrase = "Go back. That was the correct area.";
    public const string AreaPhrase = "You're in the correct area. Keep looking.";
    public const string Version = "gaze-directional-v5-return-cue";

    static readonly string[] k_Left = { LeftPhrase, "Look to your left.", "Try looking left.", "Search to your left." };
    static readonly string[] k_Right = { RightPhrase, "Look to your right.", "Try looking right.", "Search to your right." };

    static readonly string[] k_SelfRight = { RightPhrase, "Look to your right.", "Search to your right." };

    // Separate semantic direction from wording, so changing phrasing never looks
    // like a gaze reversal and accidentally interrupts the current hint.
    public static string Variant(string direction, int index, bool selfSimilar = false, bool far = false)
    {
        if (direction != LeftPhrase && direction != RightPhrase) return direction;
        if (far) return direction == LeftPhrase ? FarLeftPhrase : FarRightPhrase;
        var phrases = direction == LeftPhrase ? k_Left : selfSimilar ? k_SelfRight : k_Right;
        int slot = ((index % phrases.Length) + phrases.Length) % phrases.Length;
        return phrases[slot];
    }

    public static string[] AllPhrases() => new[]
    {
        LeftPhrase, RightPhrase, FarLeftPhrase, FarRightPhrase, "Look to your left.", "Look to your right.",
        "Try looking left.", "Try looking right.", "Search to your left.", "Search to your right.", AreaPhrase, ReturnPhrase
    };

    public static string Direction(float gazeX, float gazeZ, float targetX, float targetZ) =>
        Direction(gazeX, gazeZ, targetX, targetZ, out _);

    // Two 45-degree wall steps = 90 degrees. The shortest horizontal turn from
    // the current gaze to the object decides whether a stronger cue is needed.
    public static string Direction(float gazeX, float gazeZ, float targetX, float targetZ, out bool far)
    {
        far = false;
        double gazeLength = gazeX * gazeX + gazeZ * gazeZ;
        double targetLength = targetX * targetX + targetZ * targetZ;
        if (double.IsNaN(gazeLength) || double.IsInfinity(gazeLength) || gazeLength < 0.0001 ||
            double.IsNaN(targetLength) || double.IsInfinity(targetLength) || targetLength < 0.0001) return null;
        double cross = gazeZ * targetX - gazeX * targetZ;
        double dot = gazeX * targetX + gazeZ * targetZ;
        double degrees = Math.Atan2(cross, dot) * 180 / Math.PI;
        far = Math.Abs(degrees) > 90;
        // Aligned horizontally but outside the wall's vertical limits: avoid a false area cue.
        if (Math.Abs(degrees) < 1) return null;
        // Either turn is equal behind the user; consistently choose right to prevent jitter.
        if (Math.Abs(degrees) > 175) return RightPhrase;
        return degrees > 0 ? RightPhrase : LeftPhrase;
    }
}
