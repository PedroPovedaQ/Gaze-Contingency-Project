/// <summary>Debounced entry with a stable exit and cooldown; tracking loss never counts as exit.</summary>
public sealed class GazeZoneEntryGate
{
    double m_Entered = -1, m_Exited = -1, m_LastCue = -100;
    bool m_Latched;
    public bool InsideStable { get; private set; }
    public void Reset() { m_Entered = m_Exited = -1; m_LastCue = -100; m_Latched = false; InsideStable = false; }
    public bool Update(bool valid, bool inside, double now)
    {
        InsideStable = false;
        if (!valid) { m_Entered = m_Exited = -1; return false; }
        if (!inside)
        {
            m_Entered = -1;
            if (m_Exited < 0) m_Exited = now;
            if (now - m_Exited >= 0.5) m_Latched = false;
            return false;
        }
        m_Exited = -1;
        if (m_Entered < 0) m_Entered = now;
        InsideStable = now - m_Entered >= 0.1;
        return !m_Latched && InsideStable && now - m_LastCue >= 6;
    }
    public void MarkSpoken(double now) { m_Latched = true; m_LastCue = now; }
}
