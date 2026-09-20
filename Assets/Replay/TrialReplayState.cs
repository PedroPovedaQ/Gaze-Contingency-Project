using System;
using System.Collections.Generic;

/// <summary>Authoritative discrete state. No raycasts, clocks, audio or live study callbacks.</summary>
public sealed class TrialReplayState
{
    public TrialReplayRecord Trial { get; private set; }
    public TrialReplayRecord Sample { get; private set; }
    public TrialReplayRecord Selection { get; private set; }
    public TrialReplayRecord Audio { get; private set; }
    public bool Visible { get; private set; }
    public bool Searching { get; private set; }
    public readonly Dictionary<string, TrialReplayTransform> Transforms = new Dictionary<string, TrialReplayTransform>();

    public void Reset()
    {
        Trial = Sample = Selection = Audio = null;
        Visible = Searching = false;
        Transforms.Clear();
    }

    public void Apply(TrialReplayRecord r)
    {
        if (r.kind == "trial")
        {
            Trial = r; Sample = Selection = null; Visible = Searching = false;
            Transforms.Clear();
            foreach (var o in r.objects)
                Transforms.Add(o.id, new TrialReplayTransform { id = o.id, position = o.position, rotation = o.rotation, scale = o.scale });
        }
        else if (r.kind == "sample" && Trial != null && r.trialId == Trial.trialId)
        {
            Sample = r; Visible = r.visible; Searching = r.searchActive;
            if (r.changes != null)
                foreach (var t in r.changes) Transforms[t.id] = t;
        }
        else if (r.kind == "selection" && Trial != null && r.trialId == Trial.trialId)
            Selection = r;
        else if (r.kind == "search_start" || r.kind == "search_resumed") Visible = Searching = true;
        else if (r.kind == "search_paused" || r.kind == "transition" || r.kind == "stop" || r.kind == "end")
        { Visible = Searching = false; if (r.kind != "transition") Audio = null; }
        if (r.kind == "audio_playback_start") Audio = r;
        if (r.kind == "audio_playback_end" || r.kind == "audio_cancelled" || r.kind == "audio_failure") Audio = null;
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
    public double Duration => Records.Count == 0 ? 0 : Records[Records.Count - 1].time;
    public TrialReplayRecord Header => Records[0];
    int m_Next;
    double m_Time = -1;

    public TrialReplayTimeline(List<TrialReplayRecord> records)
    {
        Records = records;
        foreach (var r in records)
        {
            if (r.kind == "trial") Trials.Add(r);
            if (r.meshes != null) foreach (var mesh in r.meshes) Meshes[mesh.id] = mesh;
        }
    }

    public void Seek(double time)
    {
        time = Math.Max(0, Math.Min(Duration, time));
        // Backward seek reconstructs from the validated prefix. Forward playback is incremental.
        if (time < m_Time) { State.Reset(); m_Next = 0; }
        while (m_Next < Records.Count && Records[m_Next].time <= time) State.Apply(Records[m_Next++]);
        m_Time = time;
    }
}
