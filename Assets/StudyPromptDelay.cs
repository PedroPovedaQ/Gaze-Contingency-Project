using System;

/// <summary>One reminder per objective, after a continuously observed waiting period.</summary>
public sealed class StudyPromptDelay
{
    readonly double m_Delay;
    double m_Elapsed;
    bool m_Spoken;
    public StudyPromptDelay(double delay) => m_Delay = delay;
    public void Reset() { m_Elapsed = 0; m_Spoken = false; }
    public void MarkSpoken() => m_Spoken = true;
    public bool Step(bool waiting, double seconds)
    {
        if (!waiting || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0 || seconds > .2)
            m_Elapsed = 0;
        else m_Elapsed += seconds;
        return !m_Spoken && m_Elapsed >= m_Delay;
    }
}
