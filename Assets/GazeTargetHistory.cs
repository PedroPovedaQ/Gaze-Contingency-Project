using System;
using System.Collections.Generic;

/// <summary>
/// A short history of target-relative gaze errors. Crossing evidence describes
/// a target bearing, not a validated fixation or proof that the object was seen.
/// All distances/times below are pilot interaction settings.
/// </summary>
public sealed class GazeTargetHistory
{
    public const double WindowSeconds = 3, MaxGapSeconds = .1, HoldSeconds = .2;
    public const int Capacity = 384;
    const double PendingSeconds = 1.5, CooldownSeconds = 4;
    readonly Queue<Sample> m_Samples = new Queue<Sample>();
    Sample m_Last;
    string m_Direction;
    double m_Since, m_LastSpoken = double.NegativeInfinity;
    public int Count => m_Samples.Count;

    [Serializable]
    public struct Sample
    {
        public double time, inputTime;
        public long observationSequence;
        public int deviceId;
        public string eyeRecordingId;
        public float yawError, pitchError;
    }

    /// <summary>New trials reset all evidence; a gap keeps only the cue cooldown.</summary>
    public void Reset() { Clear(); m_LastSpoken = double.NegativeInfinity; }
    public void Clear() { m_Samples.Clear(); m_Last = default; m_Direction = null; }
    public Sample[] Snapshot() => m_Samples.ToArray();

    /// <summary>
    /// Positive yaw/pitch mean target right/above. Duplicate delivery timestamps
    /// do not add evidence. A missing observation interval breaks the trajectory.
    /// </summary>
    public void Observe(bool valid, Sample sample)
    {
        if (!valid || !Finite(sample.time) || !Finite(sample.inputTime) || sample.inputTime <= 0 ||
            !Finite(sample.yawError) || !Finite(sample.pitchError)) { Clear(); return; }
        if (m_Samples.Count > 0)
        {
            if (sample.eyeRecordingId != m_Last.eyeRecordingId || sample.deviceId != m_Last.deviceId || sample.time < m_Last.time ||
                sample.inputTime < m_Last.inputTime || sample.time - m_Last.time > MaxGapSeconds)
                Clear();
            else if (sample.inputTime == m_Last.inputTime) return;
        }
        while (m_Samples.Count > 0 && sample.time - m_Samples.Peek().time > WindowSeconds)
            m_Samples.Dequeue();
        // Require earlier approach-side evidence, a near-target observation, then
        // departure on the opposite side. A 179/-179 wrap cannot satisfy this.
        string direction = CrossingDirection(sample);
        if (direction != m_Direction) { m_Direction = direction; m_Since = sample.time; }
        m_Samples.Enqueue(sample);
        while (m_Samples.Count > Capacity) m_Samples.Dequeue();
        m_Last = sample;
    }

    string CrossingDirection(Sample current)
    {
        // Return yaw corrections only near target elevation, and vice versa.
        if (Math.Abs(current.pitchError) <= 3 && Math.Abs(current.yawError) >= 3 && Math.Abs(current.yawError) <= 20 &&
            Crossed(current, true))
            return current.yawError > 0 ? DirectionalHintPolicy.RightPhrase : DirectionalHintPolicy.LeftPhrase;
        if (Math.Abs(current.yawError) <= 5 && Math.Abs(current.pitchError) >= 3 && Math.Abs(current.pitchError) <= 20 &&
            Crossed(current, false))
            return current.pitchError > 0 ? DirectionalHintPolicy.UpPhrase : DirectionalHintPolicy.DownPhrase;
        return null;
    }

    bool Crossed(Sample current, bool horizontal)
    {
        float error = horizontal ? current.yawError : current.pitchError;
        bool approach = false;
        double nearAt = -1;
        foreach (var past in m_Samples)
        {
            float pastError = horizontal ? past.yawError : past.pitchError;
            float orthogonal = horizontal ? past.pitchError : past.yawError;
            if (Math.Abs(orthogonal) > (horizontal ? 3 : 5)) { approach = false; nearAt = -1; continue; }
            if (pastError * error < 0 && Math.Abs(pastError) >= 3 && Math.Abs(pastError) <= 20)
                approach = true;
            if (approach && Math.Abs(pastError) <= 1) nearAt = past.time;
        }
        return nearAt >= 0 && current.time - nearAt <= PendingSeconds;
    }

    /// <summary>Only fresh, sustained departures may speak. Returning near the target cancels them.</summary>
    public string PendingDirection(double now) => m_Direction != null && m_Samples.Count > 0 &&
        now >= m_Last.time && now - m_Last.time <= MaxGapSeconds &&
        now - m_Since >= HoldSeconds && now - m_LastSpoken >= CooldownSeconds ? m_Direction : null;

    public static bool StillBeyond(string direction, float yaw, float pitch) =>
        direction == DirectionalHintPolicy.LeftPhrase ? yaw <= -3 && yaw >= -20 && Math.Abs(pitch) <= 3 :
        direction == DirectionalHintPolicy.RightPhrase ? yaw >= 3 && yaw <= 20 && Math.Abs(pitch) <= 3 :
        direction == DirectionalHintPolicy.UpPhrase ? pitch >= 3 && pitch <= 20 && Math.Abs(yaw) <= 5 :
        direction == DirectionalHintPolicy.DownPhrase && pitch <= -3 && pitch >= -20 && Math.Abs(yaw) <= 5;

    public void MarkSpoken(double now) { m_LastSpoken = now; Clear(); }
    static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
