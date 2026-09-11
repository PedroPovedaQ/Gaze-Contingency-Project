using System;

/// <summary>Search exposure only: announcements, transitions and explicit pauses are excluded.</summary>
public sealed class StudyTrialClock
{
    double m_Accumulated;
    double m_Started;
    public bool Running { get; private set; }
    public void Begin(double now) { m_Accumulated = 0; m_Started = now; Running = true; }
    public double Elapsed(double now) => m_Accumulated + (Running ? Math.Max(0, now - m_Started) : 0);
    public void Pause(double now) { m_Accumulated = Elapsed(now); Running = false; }
    public void Resume(double now) { if (Running) return; m_Started = now; Running = true; }
}
