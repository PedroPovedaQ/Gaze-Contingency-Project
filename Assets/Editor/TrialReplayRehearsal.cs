using System;
using System.IO;
using UnityEngine;

/// <summary>A new desktop response stream; never passes through participant study logging.</summary>
public sealed class TrialReplayRehearsal : IDisposable
{
    readonly TrialReplayWriter m_Writer;
    readonly TrialReplayRecord m_Trial;
    readonly double m_Started;
    long m_Sequence;
    bool m_Closed;
    public string DirectoryPath { get; }
    public string Error => m_Writer.Error;
    public TrialReplayRehearsal(TrialReplayTimeline source, TrialReplayRecord trial, string parent, double now)
    {
        m_Trial = trial; m_Started = now;
        DirectoryPath = Path.Combine(parent, "desktop_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
        m_Writer = new TrialReplayWriter(Path.Combine(DirectoryPath, "recording.jsonl"));
        Write(new TrialReplayRecord { kind = "header", recordingId = Guid.NewGuid().ToString("N"), sourceType = "desktop_rehearsal",
            utc = DateTime.UtcNow.ToString("O"), detail = "input=mouse_click;source=" + source.Header.recordingId + ";source_trial=" + trial.trialId }, now);
        var snapshot = JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(trial));
        snapshot.participantCode = "";
        snapshot.runNumber = 0;
        snapshot.searchSeconds = 0;
        snapshot.meshes = new System.Collections.Generic.List<TrialReplayMesh>(source.Meshes.Values).ToArray();
        Write(snapshot, now);
        Write(new TrialReplayRecord { kind = "search_start", trialId = trial.trialId }, now);
    }
    void Write(TrialReplayRecord r, double now)
    {
        r.sequence = m_Sequence++; r.time = Math.Max(0, now - m_Started);
        if (!m_Writer.Append(r)) throw new IOException(m_Writer.Error ?? "Rehearsal writer closed.");
    }
    public bool Select(string id, double now)
    {
        if (m_Closed) return false;
        if (!Array.Exists(m_Trial.objects, o => o.id == id)) throw new ArgumentException("Unknown rehearsal object.");
        bool correct = id == m_Trial.targetId;
        Write(new TrialReplayRecord { kind = "selection", trialId = m_Trial.trialId, objectId = id, correct = correct,
            searchSeconds = now - m_Started, detail = "input=mouse_click" }, now);
        if (correct) Close(now, true);
        return correct;
    }
    public void Close(double now, bool complete)
    {
        if (m_Closed) return;
        Write(new TrialReplayRecord { kind = "end", trialId = m_Trial.trialId, complete = complete,
            searchSeconds = now - m_Started,
            detail = complete ? "desktop_rehearsal_completed" : "desktop_rehearsal_cancelled" }, now);
        m_Closed = true; m_Writer.Dispose();
    }
    public void Dispose()
    {
        if (!m_Closed) Close(UnityEditor.EditorApplication.timeSinceStartup, false);
    }
}
