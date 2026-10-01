using System;
using System.Collections.Generic;

/// <summary>Horizontal direction from gaze to target; Unity +X is right and +Z is forward.</summary>
public static class DirectionalHintPolicy
{
    public const string LeftPhrase = "Look left.";
    public const string RightPhrase = "Look right.";
    public const string FarLeftPhrase = "Look way left.";
    public const string FarRightPhrase = "Look way right.";
    public const string ReturnPhrase = "Go back. That was the correct area.";
    public const string AreaPhrase = "You're in the correct area. Keep looking.";
    public const string UpPhrase = "Look up.";
    public const string DownPhrase = "Look down.";
    public const string AlignedPhrase = "Right there.";
    public const string Version = "gaze-directional-v7-shared-coarse-fine";

    static readonly string[] k_Left = { LeftPhrase, "Look to your left.", "A little more left.", "Search to your left.",
        "Keep looking left.", "Look more to your left.", "Continue looking left.", "Scan to your left." };
    static readonly string[] k_Right = { RightPhrase, "Look to your right.", "A little more right.", "Search to your right.",
        "Keep looking right.", "Look more to your right.", "Continue looking right.", "Scan to your right." };
    static readonly string[] k_FarLeft = { "Turn to your left.", "Keep turning left.", "A bit farther left.", "Turn farther left." };
    static readonly string[] k_FarRight = { "Turn to your right.", "Keep turning right.", "A bit farther right.", "Turn farther right." };
    static readonly string[] k_Up = { UpPhrase, "A little higher.", "Look slightly up.", "Up a little more." };
    static readonly string[] k_Down = { DownPhrase, "A little lower.", "Look slightly down.", "Down a little more." };
    static readonly string[] k_Aligned = { AlignedPhrase, "Right around there.", "Keep looking right there.", "You should see it there." };
    static readonly string[] k_Area = { AreaPhrase, "This is the correct area. Keep looking.",
        "Stay in this area and keep looking.", "Keep searching in this area." };
    static readonly string[] k_Return = { ReturnPhrase, "Go back to that area.",
        "Look back toward the area you just left.", "Return to the area you were looking at." };

    // Select wording separately from direction, so a new variant never triggers
    // stale-direction cancellation. The caller logs the phrase actually spoken.
    public static string Variant(string direction, int index, bool selfSimilar = false, bool far = false)
    {
        string[] phrases;
        if (direction == AreaPhrase) phrases = k_Area;
        else if (direction == ReturnPhrase) phrases = k_Return;
        else if (direction == UpPhrase) phrases = k_Up;
        else if (direction == DownPhrase) phrases = k_Down;
        else if (direction == AlignedPhrase) phrases = k_Aligned;
        else if (direction == LeftPhrase) phrases = far ? k_FarLeft : k_Left;
        else if (direction == RightPhrase) phrases = far ? k_FarRight : k_Right;
        else return direction;
        int slot = ((index % phrases.Length) + phrases.Length) % phrases.Length;
        return phrases[slot];
    }

    public static string[] AllPhrases()
    {
        var phrases = new List<string>();
        foreach (var group in new[] { k_Left, k_Right, k_FarLeft, k_FarRight, k_Area, k_Return, k_Up, k_Down, k_Aligned })
            phrases.AddRange(group);
        return phrases.ToArray();
    }

    public static string Direction(float gazeX, float gazeZ, float targetX, float targetZ) =>
        Direction(gazeX, gazeZ, targetX, targetZ, out _);

    // Beyond one 45-degree wall step, ask for a chair turn. The shortest turn from
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
        far = Math.Abs(degrees) > 45;
        // Aligned horizontally but outside the wall's vertical limits: avoid a false area cue.
        if (Math.Abs(degrees) < 1) return null;
        // Either turn is equal behind the user; consistently choose right to prevent jitter.
        if (Math.Abs(degrees) > 175) return RightPhrase;
        return degrees > 0 ? RightPhrase : LeftPhrase;
    }
}
