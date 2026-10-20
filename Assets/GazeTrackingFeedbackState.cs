using System;

/// <summary>Display timing only. Never changes eye validity, trial timing or readiness.</summary>
public sealed class GazeTrackingFeedbackState
{
    public bool Visible { get; private set; }
    double m_InvalidSince = -1, m_ValidSince = -1;

    // One second filters ordinary blinks; stable recovery prevents a flashing banner.
    public void Step(bool eligible, bool valid, double now)
    {
        if (!eligible || double.IsNaN(now) || double.IsInfinity(now)) { Reset(); return; }
        if (valid)
        {
            m_InvalidSince = -1;
            if (m_ValidSince < 0 || now < m_ValidSince) m_ValidSince = now;
            if (now - m_ValidSince >= .35) Visible = false;
        }
        else
        {
            m_ValidSince = -1;
            if (m_InvalidSince < 0 || now < m_InvalidSince) m_InvalidSince = now;
            if (now - m_InvalidSince >= 1) Visible = true;
        }
    }

    public void Reset() { Visible = false; m_InvalidSince = m_ValidSince = -1; }

    public static string Reason(bool available, bool fresh, bool nativeFresh,
        bool leftValid, bool rightValid, bool headValid)
    {
        if (!available) return "Eye-tracking data is unavailable.";
        if (!fresh) return "Eye-tracking updates have stopped.";
        if (!headValid) return "Headset position tracking is unavailable.";
        if (nativeFresh && !leftValid && !rightValid) return "Neither eye is being tracked.";
        if (nativeFresh && !leftValid) return "Your left eye is not being tracked.";
        if (nativeFresh && !rightValid) return "Your right eye is not being tracked.";
        return "The headset is not providing usable gaze data.";
    }
}
