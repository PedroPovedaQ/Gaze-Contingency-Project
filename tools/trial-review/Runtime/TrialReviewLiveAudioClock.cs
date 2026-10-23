using System;
using System.Collections.Generic;

// Keep scene, subtitles and audio on one recorded clock. Catch up through silence,
// never by jumping over received speech. Explicit join/resume may start at the live edge.
public sealed class TrialReviewLiveAudioClock
{
    public const double AssetWaitSeconds = 5;
    readonly List<TrialReplayRecord> m_Starts = new List<TrialReplayRecord>();
    readonly HashSet<long> m_Unavailable = new HashSet<long>();
    int m_Indexed;
    long m_Waiting = -1;
    double m_WaitStarted;
    public bool WaitingForAudio => m_Waiting >= 0;
    public int UnavailableCount => m_Unavailable.Count;
    public void Resume() { m_Waiting = -1; }

    public double Advance(double current, double requested, double step, double now,
        TrialReplayTimeline timeline, TrialReplayAudioPlan plan)
    {
        for (; m_Indexed < timeline.Records.Count; m_Indexed++)
            if (timeline.Records[m_Indexed].kind == "audio_playback_start") m_Starts.Add(timeline.Records[m_Indexed]);
        double next = Math.Max(current, requested);
        foreach (var segment in plan.Segments)
            if (segment.Contains(current, long.MaxValue))
                next = Math.Min(next, current + Math.Max(0, step));
        foreach (var start in m_Starts)
        {
            if (start.time < current) continue;
            if (start.time > next) break;
            if (m_Unavailable.Contains(start.sequence)) continue;
            TrialReplayAudioPlan.Segment segment = null;
            foreach (var candidate in plan.Segments)
                if (candidate.Start.sequence == start.sequence) { segment = candidate; break; }
            if (start.time > current) return start.time; // Play even a whole phrase received in one chunk.
            if (start.time == current && segment == null)
            {
                if (m_Waiting != start.sequence) { m_Waiting = start.sequence; m_WaitStarted = now; }
                if (now - m_WaitStarted < AssetWaitSeconds) return current;
                m_Unavailable.Add(start.sequence); m_Waiting = -1; // Report the gap; do not invent audio.
                continue;
            }
            if (m_Waiting == start.sequence)
            { m_Waiting = -1; return current; } // Newly arrived clip starts at offset zero.
        }
        return next;
    }
}
