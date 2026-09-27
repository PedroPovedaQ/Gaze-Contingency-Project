using System;

/// <summary>Continuous between-trial gaze charge, independent of search-object selection.</summary>
public sealed class StudyReadinessCharge
{
    public float Progress { get; private set; }
    public void Reset() => Progress = 0;

    public bool Step(bool validInside, bool paused, bool announcementReady, float seconds, float requiredSeconds)
    {
        // A long frame gap cannot count as continuously observed gaze.
        if (!validInside || paused || seconds < 0 || seconds > 0.2f ||
            float.IsNaN(seconds) || float.IsInfinity(seconds)) Reset();
        else Progress = Math.Min(1f, Progress + seconds / Math.Max(0.1f, requiredSeconds));
        return Progress >= 1f && announcementReady && !paused && validInside;
    }
}
