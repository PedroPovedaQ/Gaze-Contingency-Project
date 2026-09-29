/// <summary>A brief stable visit followed by a sustained exit permits one recent return cue.</summary>
public sealed class GazeReturnCueGate
{
    double m_Entered = -1, m_Exited = -1, m_LastCue = -100;
    bool m_Armed;
    public bool IsDebouncingExit { get; private set; }

    public void Reset()
    {
        m_Entered = m_Exited = -1;
        m_LastCue = -100;
        m_Armed = IsDebouncingExit = false;
    }

    public bool Update(bool valid, bool inside, double now)
    {
        IsDebouncingExit = false;
        if (!valid)
        {
            // A blink/tracking gap cannot establish that the person passed the area.
            m_Entered = m_Exited = -1;
            m_Armed = false;
            return false;
        }
        if (inside)
        {
            m_Exited = -1;
            if (m_Entered < 0) m_Entered = now;
            if (now - m_Entered >= 0.1) m_Armed = true;
            return false;
        }
        m_Entered = -1;
        if (!m_Armed) return false;
        if (m_Exited < 0) m_Exited = now;
        if (now - m_Exited > 2) { m_Armed = false; return false; }
        IsDebouncingExit = now - m_Exited < 0.25;
        return IsPending(now);
    }

    public bool IsPending(double now) => m_Armed && m_Exited >= 0 &&
        now - m_Exited >= 0.25 && now - m_Exited <= 2 && now - m_LastCue >= 6;

    public void MarkSpoken(double now) { m_Armed = false; m_LastCue = now; }
}
