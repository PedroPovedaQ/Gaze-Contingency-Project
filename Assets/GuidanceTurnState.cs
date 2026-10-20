using System;

/// <summary>Retains a turn across the rear seam and rewards new 45-degree progress only.</summary>
public sealed class GuidanceTurnState
{
    string m_Route, m_SpokenDirection;
    float m_NextMilestone;

    public void Reset() { m_Route = m_SpokenDirection = null; m_NextMilestone = 0; }
    public void Prime(string direction) { Reset(); m_Route = direction; }

    // The actual target bearing includes its lateral position on the wall. Once
    // chosen, small movements around +/-180 must not reverse that instruction.
    public string Resolve(float yaw, bool inside)
    {
        if (inside) { Reset(); return null; }
        if (Math.Abs(yaw) < 1) return null;
        string direction = yaw >= 0 ? DirectionalHintPolicy.RightPhrase : DirectionalHintPolicy.LeftPhrase;
        if (m_Route == null || Math.Abs(yaw) < 150) m_Route = direction;
        return m_Route;
    }

    // Async revalidation cannot change the route or consume progress.
    public string Peek(float yaw, bool inside)
    {
        if (inside || Math.Abs(yaw) < 1) return null;
        return m_Route == null || Math.Abs(yaw) < 150
            ? (yaw >= 0 ? DirectionalHintPolicy.RightPhrase : DirectionalHintPolicy.LeftPhrase) : m_Route;
    }

    public float Remaining(float yaw) => m_Route != null && Math.Abs(yaw) >= 150 &&
        (yaw >= 0) != (m_Route == DirectionalHintPolicy.RightPhrase) ? 360 - Math.Abs(yaw) : Math.Abs(yaw);

    public bool ProgressDue(string direction, float remaining) => direction == m_SpokenDirection &&
        m_NextMilestone > 0 && remaining <= m_NextMilestone;

    // Advance only when a cue is issued. Backtracking and crossing the same
    // milestone twice cannot earn another "keep going". Skipped milestones coalesce.
    public void MarkSpoken(string direction, float remaining)
    {
        if (direction != m_SpokenDirection || ProgressDue(direction, remaining))
            m_NextMilestone = Math.Max(0, (float)Math.Floor((remaining - .01f) / 45) * 45);
        m_SpokenDirection = direction;
    }
}
