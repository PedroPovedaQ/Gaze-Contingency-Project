using System;

/// <summary>Familiarization timing only; a sustained guidance response is not a measured fixation.</summary>
public sealed class ActivationCheckProgress
{
    public const double HoldSeconds = .35;
    public int Step { get; private set; }
    public bool Finished => Step >= 3;
    public bool Ready => !Finished && m_Hold >= HoldSeconds;
    double m_Hold, m_Last = double.NaN;

    public void ResetGaze() { m_Hold = 0; m_Last = double.NaN; }
    public void Observe(bool usable, int gazeObject, double now)
    {
        if (Finished || !usable || gazeObject != Step || double.IsNaN(now) || double.IsInfinity(now))
        { ResetGaze(); return; }
        if (!double.IsNaN(m_Last) && now >= m_Last && now - m_Last <= .1)
            m_Hold += now - m_Last;
        else m_Hold = 0;
        m_Last = now;
    }
    public string Select(int selected)
    {
        if (Finished) return "already_complete";
        if (selected != Step) return "wrong_object";
        if (!Ready) return "gaze_not_ready";
        Step++; ResetGaze(); return "correct";
    }
}
