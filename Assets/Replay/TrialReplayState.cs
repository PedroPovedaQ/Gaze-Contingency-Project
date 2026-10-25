using System;
using System.Collections.Generic;

/// <summary>Authoritative discrete state. No raycasts, clocks, audio or live study callbacks.</summary>
public sealed class TrialReplayState
{
    public TrialReplayRecord Trial { get; private set; }
    public TrialReplayRecord Sample { get; private set; }
    public TrialReplayRecord Selection { get; private set; }
    public TrialReplayRecord Readiness { get; private set; }
    public TrialReplayRecord PoseBoundary { get; private set; }
    // A display freshness rule, not a fixation threshold or acquisition-rate claim.
    public const double DefaultMaxSampleAgeSeconds = 0.1;
    public bool Visible { get; private set; }
    public bool Searching { get; private set; }
    public readonly Dictionary<string, TrialReplayTransform> Transforms = new Dictionary<string, TrialReplayTransform>();

    public void Reset()
    {
        Trial = Sample = Selection = Readiness = PoseBoundary = null;
        Visible = Searching = false;
        Transforms.Clear();
    }

    public void Apply(TrialReplayRecord r)
    {
        if (r.kind == "trial" || r.kind == "transition" || r.kind == "search_start" ||
            r.kind == "search_resumed" || r.kind == "search_paused" || r.kind == "application_paused" ||
            r.kind == "trial_timeout" || r.kind == "stop" || r.kind == "end") PoseBoundary = r;
        if (r.kind == "transition" || r.kind == "end" || r.kind == "stop" || r.kind == "trial_timeout") Readiness = null;
        if (r.kind == "sample" || r.kind.StartsWith("readiness_", StringComparison.Ordinal)) Readiness = r;
        if (r.kind == "trial")
        {
            Trial = r; Sample = Selection = Readiness = null; Visible = Searching = false;
            Transforms.Clear();
            foreach (var o in r.objects)
                Transforms.Add(o.id, new TrialReplayTransform { id = o.id, position = o.position, rotation = o.rotation, scale = o.scale });
        }
        else if (r.kind == "sample" && Trial != null && r.trialId == Trial.trialId)
        {
            Sample = r; Visible = r.visible; Searching = r.searchActive;
        }
        else if (r.kind == "selection" && Trial != null && r.trialId == Trial.trialId)
        {
            Selection = r;
            // Even an unavailable selection pose replaces an older valid one.
            // Schema 1 selections did not carry pose observations.
            if (r.schema >= 2) Sample = r;
            if (r.correct) Visible = Searching = false;
        }
        else if (r.kind == "search_start" || r.kind == "search_resumed") Visible = Searching = true;
        else if (r.kind == "search_paused" || r.kind == "application_paused" || r.kind == "transition" || r.kind == "trial_timeout" || r.kind == "stop" || r.kind == "end")
        { Visible = Searching = false; }
        if ((r.kind == "sample" || r.kind == "selection" || r.kind == "readiness_completed") && Trial != null && r.trialId == Trial.trialId && r.changes != null)
            foreach (var transform in r.changes) Transforms[transform.id] = transform;
    }

    public bool HasCurrentSample(double time, double maxAge = DefaultMaxSampleAgeSeconds)
    {
        return Sample != null && Trial != null && Sample.trialId == Trial.trialId &&
            (PoseBoundary == null || Sample.sequence > PoseBoundary.sequence) &&
            IsFresh(Sample, time, maxAge);
    }

    public static bool IsFresh(TrialReplayRecord record, double time, double maxAge = DefaultMaxSampleAgeSeconds)
    {
        return record != null && !double.IsNaN(time) && !double.IsInfinity(time) &&
            !double.IsNaN(maxAge) && !double.IsInfinity(maxAge) && maxAge > 0 &&
            time >= record.time && time <= record.time + maxAge;
    }
}

public sealed class TrialReplayTimeline
{
    public readonly List<TrialReplayRecord> Records;
    public readonly List<TrialReplayRecord> Trials = new List<TrialReplayRecord>();
    public readonly Dictionary<string, TrialReplayMesh> Meshes = new Dictionary<string, TrialReplayMesh>();
    public readonly TrialReplayState State = new TrialReplayState();
    public string Warning { get; internal set; }
    public string DirectoryPath { get; internal set; }
    public string FilePath { get; internal set; }
    public int CurrentSourceLine => m_Next;
    public int SampleSourceLine { get; private set; }
    public TrialReplayRecord CurrentRecord => m_Next > 0 ? Records[m_Next - 1] : null;
    public double Duration => Records.Count == 0 ? 0 : Records[Records.Count - 1].time;
    public TrialReplayRecord Header => Records[0];
    int m_Next;
    readonly List<int> m_TrialIndices = new List<int>();
    double m_Time = -1;
    long m_MaxSequence = long.MaxValue;

    public TrialReplayTimeline(List<TrialReplayRecord> records)
    {
        Records = records;
        for (int i = 0; i < records.Count; i++) Index(records[i], i);
    }

    void Index(TrialReplayRecord r, int index)
    {
        if (r.kind == "trial") { Trials.Add(r); m_TrialIndices.Add(index); }
        if (r.meshes != null) foreach (var mesh in r.meshes) Meshes[mesh.id] = mesh;
    }

    // Caller must validate through TrialReplayFile.Validator before appending.
    public void AppendValidated(TrialReplayRecord record)
    {
        Index(record, Records.Count); Records.Add(record);
    }

    public void Seek(double time, long maxSequence = long.MaxValue)
    {
        if (double.IsNaN(time) || double.IsInfinity(time)) throw new ArgumentOutOfRangeException(nameof(time));
        time = Math.Max(0, Math.Min(Duration, time));
        // Trial snapshots contain the complete object state. Earlier trials cannot
        // affect this trial, so jump to its saved snapshot on a rewind/long seek.
        // Keep absolute record indices: exported source lines must not be rebased.
        int low = 0, high = m_TrialIndices.Count;
        while (low < high)
        {
            int middle = (low + high) / 2;
            var trial = Records[m_TrialIndices[middle]];
            if (trial.time < time || (trial.time == time && trial.sequence <= maxSequence)) low = middle + 1;
            else high = middle;
        }
        int snapshot = low == 0 ? 0 : m_TrialIndices[low - 1];
        if (time < m_Time || (time == m_Time && maxSequence < m_MaxSequence) || snapshot > m_Next)
        { State.Reset(); m_Next = snapshot; SampleSourceLine = 0; }
        while (m_Next < Records.Count && (Records[m_Next].time < time ||
            (Records[m_Next].time == time && Records[m_Next].sequence <= maxSequence)))
        {
            var record = Records[m_Next++]; State.Apply(record);
            if (State.Sample == record) SampleSourceLine = m_Next;
            else if (State.Sample == null) SampleSourceLine = 0;
        }
        m_Time = time; m_MaxSequence = maxSequence;
    }
}
