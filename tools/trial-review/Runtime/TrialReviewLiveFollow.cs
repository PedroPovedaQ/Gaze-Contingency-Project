using System;

/// <summary>Live transport can be connected while the headset is asleep or replaying old bytes.</summary>
public sealed class TrialReviewLiveFollow
{
    public const double BufferSeconds = .2;
    public const double SignalTimeoutSeconds = 2;
    // A short transport-buffer underrun freezes the scene cursor, not a WAV
    // already playing. Real disconnect/pause/staleness clears liveSignalActive.
    public static bool AudioEnabled(bool playing, bool following, bool liveSignalActive) =>
        playing || following && liveSignalActive;
    double m_LastTime, m_SampleTime;
    bool m_TrackedSample, m_Paused;
    string m_PauseReason;
    bool m_HasSample;
    public bool CanDrain(double cursor) => m_Paused && cursor < m_LastTime;

    public void Observe(TrialReplayRecord record)
    {
        m_LastTime = record.time;
        if (record.kind == "sample")
        {
            m_HasSample = true;
            m_SampleTime = record.time;
            m_TrackedSample = record.headAvailable && record.headTracked == 1;
            m_Paused = false;
        }
        else if (record.kind == "application_paused" || record.kind == "stop" || record.kind == "end")
        {
            m_Paused = true;
            m_PauseReason = record.kind == "application_paused" ? "Headset app paused" :
                record.kind == "end" ? "Run complete" : "Session stopped";
        }
    }

    public string WaitingReason(bool connected, double nowUtc, double sourceUpdatedUtc, double pendingBytes, bool caughtUp)
    {
        if (!connected) return "Waiting for Wi-Fi connection";
        if (!caughtUp || pendingBytes > 0) return "Catching up with headset";
        if (m_Paused) return m_PauseReason;
        if (!m_HasSample) return "Waiting for a trial";
        if (!m_TrackedSample) return "Waiting for head tracking";
        if (sourceUpdatedUtc <= 0 || nowUtc - sourceUpdatedUtc >= SignalTimeoutSeconds)
            return "Waiting for fresh headset samples";
        return "Joining live view";
    }

    public bool Ready(bool connected, double nowUtc, double sourceUpdatedUtc, double pendingBytes, bool caughtUp, bool following = false)
    {
        double age = nowUtc - sourceUpdatedUtc;
        // Catch up before joining. Once following, a partially parsed fresh network
        // chunk must not flash the waiting screen or repeatedly restart the audio.
        return connected && (caughtUp || following) && pendingBytes == 0 && sourceUpdatedUtc > 0 &&
            age >= 0 && age < SignalTimeoutSeconds && m_TrackedSample && !m_Paused && m_LastTime - m_SampleTime < 1;
    }

    // Only joining/resuming may seek backwards. Normal following never repeatedly
    // rewinds to the buffer edge when a network chunk arrives.
    public static double Cursor(double current, double first, double latest, bool joining)
    {
        double edge = Math.Max(first, latest - BufferSeconds);
        return joining || latest - current > BufferSeconds + .75 ? edge : current;
    }
}
