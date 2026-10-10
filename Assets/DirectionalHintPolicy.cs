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
    public const string PointingPhrase = "You're on it!";
    public const string LookingPhrase = "Yep, that's it!";
    public const string SelectionReminderPhrase = "Aim at it with your right controller and press the trigger.";
    public const double LookingHoldSeconds = .1;
    public const double LookingExitSeconds = .15;
    public const string UpLeftPhrase = "Look up and a little to your left.";
    public const string UpRightPhrase = "Look up and a little to your right.";
    public const string DownLeftPhrase = "Look down and a little to your left.";
    public const string DownRightPhrase = "Look down and a little to your right.";
    public const string KeepGoingPhrase = "Keep going.";
    public const string Version = "gaze-directional-v20-bounded-jev-fade";
    static readonly string[] k_AreaLead = { "Right area.", "This is the right area.",
        "You're in the right area.", "Stay in this area." };

    // One prepared clip: there is no cadence delay between arrival and refinement.
    public static string AreaEntry(string direction, int index, float verticalFraction = 0) =>
        k_AreaLead[((index % k_AreaLead.Length) + k_AreaLead.Length) % k_AreaLead.Length] + " " +
        Variant(direction, 0, verticalFraction: verticalFraction);

    // Fractions are signed target-minus-gaze height / actual wall height.
    // Exact boundaries belong to the smaller band; no angular approximation.
    public static int VerticalBand(float fraction) => float.IsNaN(fraction) ? 1 :
        Math.Abs(fraction) <= .2f ? 0 : Math.Abs(fraction) <= .5f ? 1 : 2;
    public static bool HasVertical(string direction) => HasUp(direction) || HasDown(direction);

    public static string WithinArea(float yawError, float pitchError, string previous = null)
    {
        // Separate arrival/departure thresholds stop small eye movements toggling
        // between aligned and directional speech. Raw gaze and metrics are unchanged.
        if (previous == AlignedPhrase && Math.Abs(pitchError) <= 4.5f && Math.Abs(yawError) <= 7) return AlignedPhrase;
        bool up = pitchError > (HasUp(previous) ? 1.5f : 3f);
        bool down = pitchError < -(HasDown(previous) ? 1.5f : 3f);
        bool left = yawError < -(HasLeft(previous) ? 3f : 5f);
        bool right = yawError > (HasRight(previous) ? 3f : 5f);
        if (up) return left ? UpLeftPhrase : right ? UpRightPhrase : UpPhrase;
        if (down) return left ? DownLeftPhrase : right ? DownRightPhrase : DownPhrase;
        return left ? LeftPhrase : right ? RightPhrase : AlignedPhrase;
    }
    static bool HasUp(string d) => d == UpPhrase || d == UpLeftPhrase || d == UpRightPhrase;
    static bool HasDown(string d) => d == DownPhrase || d == DownLeftPhrase || d == DownRightPhrase;
    static bool HasLeft(string d) => d == LeftPhrase || d == UpLeftPhrase || d == DownLeftPhrase;
    static bool HasRight(string d) => d == RightPhrase || d == UpRightPhrase || d == DownRightPhrase;
    static bool IsDiagonal(string d) => (HasUp(d) || HasDown(d)) && (HasLeft(d) || HasRight(d));

    public static string Turn(string direction, float remaining, int index) => remaining >= 90
        ? "Turn way " + (direction == LeftPhrase ? "left." : "right.")
        : Variant(direction, index, far: remaining > 45);

    public static string Progress(int index) => k_Progress[((index % k_Progress.Length) + k_Progress.Length) % k_Progress.Length];

    public static string ReturnToTarget(string direction)
    {
        string vertical = HasUp(direction) ? "up" : HasDown(direction) ? "down" : "";
        string horizontal = HasLeft(direction) ? "left" : HasRight(direction) ? "right" : "";
        string location = vertical + (vertical.Length > 0 && horizontal.Length > 0 ? " and " : "") + horizontal;
        return "Go back " + location + ".";
    }

    public static string ReturnToArea(string direction) => "Go back " +
        (direction == LeftPhrase ? "left." : "right.");

    public static float DirectionError(string direction, float yaw, float pitch) =>
        IsDiagonal(direction) ? (float)Math.Sqrt(yaw * yaw + pitch * pitch) :
        direction == UpPhrase || direction == DownPhrase ? Math.Abs(pitch) : Math.Abs(yaw);

    // Encourage actual progress, not standing still or moving the wrong way.
    public static string FollowUp(string direction, string previous, float error, float previousError, int index, bool far,
        bool continuationUsed = false, float verticalFraction = 0) =>
        !continuationUsed && direction == previous && direction != AlignedPhrase && error <= previousError - 2
            ? KeepGoingPhrase : Variant(direction, index, false, far, verticalFraction);

    public static string Opening(string direction) => direction == LeftPhrase ? "The object is to the left." :
        direction == RightPhrase ? "The object is to the right." :
        direction == UpPhrase ? "The object is above where you're looking." :
        direction == DownPhrase ? "The object is below where you're looking." :
        "The object is ahead of you. Keep looking here.";

    public static string PassedTarget(string direction, float verticalFraction = 0) => direction == LeftPhrase ? "Go back a little to the left." :
        direction == RightPhrase ? "Go back a little to the right." :
        "Go back " + (VerticalBand(verticalFraction) == 2 ? "way " : "") +
        (direction == UpPhrase ? "up" : "down") + (VerticalBand(verticalFraction) == 0 ? " a little." : ".");

    // Relative to the current valid eye ray, not the headset's original forward.
    public static string ReadinessReminder(float yaw, float pitch)
    {
        string horizontal = Math.Abs(yaw) <= 5 ? "" : yaw > 0 || Math.Abs(yaw) > 175 ? "right" : "left";
        string vertical = Math.Abs(pitch) <= 5 ? "" : pitch > 0 ? "above" : "below";
        string location = horizontal.Length == 0
            ? (vertical.Length == 0 ? "ahead of you" : vertical + " you")
            : (vertical.Length == 0 ? "to your " : vertical + " and to your ") + horizontal;
        return "Look for the cross " + location + " and hold your gaze on it to begin the next round.";
    }

    static readonly string[] k_Left = { LeftPhrase, "Look to your left.", "A little more left.", "Search to your left.",
        "Keep looking left.", "Look more to your left.", "Continue looking left.", "Scan to your left." };
    static readonly string[] k_Right = { RightPhrase, "Look to your right.", "A little more right.", "Search to your right.",
        "Keep looking right.", "Look more to your right.", "Continue looking right.", "Scan to your right." };
    static readonly string[] k_FarLeft = { "Turn to your left.", "Keep turning left.", "A bit farther left.", "Turn farther left." };
    static readonly string[] k_FarRight = { "Turn to your right.", "Keep turning right.", "A bit farther right.", "Turn farther right." };
    static readonly string[] k_Aligned = { AlignedPhrase, "Right around there.", "Keep looking right there.", "You should see it there." };
    static readonly string[] k_Looking = { LookingPhrase, "Yes, that's it!", "That's the one!", "You've got it!" };
    static readonly string[] k_Progress = { KeepGoingPhrase, "That's it, keep turning.", "Keep going that way." };
    static readonly string[] k_Area = { AreaPhrase, "This is the correct area. Keep looking.",
        "Stay in this area and keep looking.", "Keep searching in this area." };
    static readonly string[] k_Return = { ReturnPhrase, "Go back to that area.",
        "Look back toward the area you just left.", "Return to the area you were looking at." };

    // Select wording separately from direction, so a new variant never triggers
    // stale-direction cancellation. The caller logs the phrase actually spoken.
    public static string Variant(string direction, int index, bool selfSimilar = false, bool far = false, float verticalFraction = 0)
    {
        if (HasVertical(direction))
        {
            string vertical = HasUp(direction) ? "higher" : "lower";
            int band = VerticalBand(verticalFraction), variant = ((index % 3) + 3) % 3;
            string cue = band == 0
                ? (variant == 0 ? "A little " : variant == 1 ? "Look a little " : "Just a little ") + vertical
                : band == 1
                    ? (variant == 0 ? "Look " : variant == 1 ? "Look further " : "Keep looking ") + vertical
                    : (variant == 0 ? "Look way " : variant == 1 ? "Much " : "Look much ") + vertical;
            if (IsDiagonal(direction)) cue += " and " + (HasLeft(direction) ? "left" : "right");
            return cue + ".";
        }
        string[] phrases;
        if (direction == AreaPhrase) phrases = k_Area;
        else if (direction == ReturnPhrase) phrases = k_Return;
        else if (direction == AlignedPhrase) phrases = k_Aligned;
        else if (direction == LookingPhrase) phrases = k_Looking;
        else if (direction == LeftPhrase) phrases = far ? k_FarLeft : k_Left;
        else if (direction == RightPhrase) phrases = far ? k_FarRight : k_Right;
        else return direction;
        int slot = ((index % phrases.Length) + phrases.Length) % phrases.Length;
        return phrases[slot];
    }

    public static string[] AllPhrases()
    {
        var phrases = new List<string>();
        foreach (var group in new[] { k_Left, k_Right, k_FarLeft, k_FarRight, k_Area, k_Return, k_Aligned, k_Looking })
            phrases.AddRange(group);
        foreach (var direction in new[] { LeftPhrase, RightPhrase, UpPhrase, DownPhrase, AlignedPhrase })
            phrases.Add(Opening(direction));
        foreach (float fraction in new[] { .1f, .3f, .7f })
        {
            foreach (var direction in new[] { LeftPhrase, RightPhrase, UpPhrase, DownPhrase })
                phrases.Add(PassedTarget(direction, fraction));
            foreach (var direction in new[] { LeftPhrase, RightPhrase, UpPhrase, DownPhrase, AlignedPhrase, UpLeftPhrase, UpRightPhrase, DownLeftPhrase, DownRightPhrase })
                for (int i = 0; i < k_AreaLead.Length; i++) phrases.Add(AreaEntry(direction, i, fraction));
            foreach (var direction in new[] { UpPhrase, DownPhrase, UpLeftPhrase, UpRightPhrase, DownLeftPhrase, DownRightPhrase })
                for (int i = 0; i < 3; i++) phrases.Add(Variant(direction, i, verticalFraction: fraction));
        }
        phrases.Add(LookingPhrase);
        phrases.Add(PointingPhrase);
        phrases.Add(KeepGoingPhrase);
        phrases.AddRange(k_Progress);
        foreach (var direction in new[] { LeftPhrase, RightPhrase })
        { phrases.Add(Turn(direction, 180, 0)); phrases.Add(ReturnToArea(direction)); }
        foreach (var direction in new[] { LeftPhrase, RightPhrase, UpPhrase, DownPhrase, UpLeftPhrase, UpRightPhrase, DownLeftPhrase, DownRightPhrase })
            phrases.Add(ReturnToTarget(direction));
        phrases.Add(SelectionReminderPhrase);
        foreach (float yaw in new[] { -30f, 0f, 30f })
            foreach (float pitch in new[] { -20f, 0f, 20f }) phrases.Add(ReadinessReminder(yaw, pitch));
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
        double degrees = SignedYaw(gazeX, gazeZ, targetX, targetZ);
        far = Math.Abs(degrees) > 45;
        // Aligned horizontally but outside the wall's vertical limits: avoid a false area cue.
        if (Math.Abs(degrees) < 1) return null;
        return degrees > 0 ? RightPhrase : LeftPhrase;
    }

    // Subtract bearings so an exact 0->180 tie is right and 180->0 is left.
    // All non-ties use the shorter path to the actual object, not its wall center.
    public static float SignedYaw(float gazeX, float gazeZ, float targetX, float targetZ)
    {
        double angle = (Math.Atan2(targetX, targetZ) - Math.Atan2(gazeX, gazeZ)) * 180 / Math.PI;
        if (angle > 180) angle -= 360;
        if (angle < -180) angle += 360;
        return (float)angle;
    }
}
