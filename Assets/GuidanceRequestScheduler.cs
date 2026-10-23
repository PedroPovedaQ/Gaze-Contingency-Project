using System;
using System.Collections.Generic;
using System.Threading;

public enum GuidanceRequestKind { Catalog, Transmission, Interruption }

/// <summary>Owns cancellable decision scopes and the single unsounded guidance cue.
/// Audio already playing is deliberately outside this class. Unity alone calls
/// TryTakePlayback immediately before Play, then invokes its commit after Play.</summary>
public sealed class GuidanceRequestScheduler
{
    readonly Dictionary<GuidanceRequestKind, CancellationTokenSource> m_Requests = new Dictionary<GuidanceRequestKind, CancellationTokenSource>();
    Func<bool> m_PlaybackRelevant;
    Action m_PlaybackCommitted;
    public bool PlaybackQueued => m_PlaybackRelevant != null;

    public CancellationTokenSource Begin(GuidanceRequestKind kind)
    {
        Cancel(kind);
        var request = new CancellationTokenSource(); m_Requests[kind] = request;
        return request;
    }
    public void Complete(GuidanceRequestKind kind, CancellationTokenSource request)
    {
        if (!m_Requests.TryGetValue(kind, out var current) || !ReferenceEquals(current, request)) return;
        Cancel(kind);
    }
    public void Cancel(GuidanceRequestKind kind)
    {
        if (!m_Requests.TryGetValue(kind, out var request)) return;
        m_Requests.Remove(kind); request.Cancel(); request.Dispose();
    }
    public void CancelAll()
    {
        foreach (GuidanceRequestKind kind in Enum.GetValues(typeof(GuidanceRequestKind))) Cancel(kind);
        CancelPlayback();
    }
    public void QueuePlayback(Func<bool> relevant, Action committed)
    { m_PlaybackRelevant = relevant; m_PlaybackCommitted = committed; }
    public void CancelPlayback() { m_PlaybackRelevant = null; m_PlaybackCommitted = null; }
    public bool TryTakePlayback(out Action committed)
    {
        committed = null;
        bool ready = m_PlaybackRelevant == null || m_PlaybackRelevant();
        if (ready) committed = m_PlaybackCommitted;
        CancelPlayback();
        return ready;
    }
}
