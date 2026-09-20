using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public static class TrialReplayFile
{
    public const int Schema = 1;
    public const int MaxRecords = 2000000;
    public static string AssetPath(string directory, string relative)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains("\\") || relative.Contains(":"))
            throw new InvalidDataException("Invalid replay asset path.");
        string root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (!path.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException("Replay asset escapes its folder.");
        // Reject links in every existing segment, including directories.
        string current = directory;
        foreach (var part in relative.Split('/'))
        {
            if (part == "." || part == ".." || part.Length == 0) throw new InvalidDataException("Invalid replay asset segment.");
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked replay assets are not supported.");
        }
        return path;
    }

    public static TrialReplayTimeline Read(string file)
    {
        var records = new List<TrialReplayRecord>();
        string warning = null;
        var ids = new HashSet<string>();
        var trials = new HashSet<string>();
        var meshes = new HashSet<string>();
        string trial = null, targetId = null;
        long sequence = -1;
        double time = -1;
        var info = new FileInfo(file);
        if (info.Length > 1024L * 1024 * 1024) throw new InvalidDataException("Replay exceeds the 1 GiB viewer limit; split it before opening.");
        using (var reader = new StreamReader(file))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length > 16000000) throw new InvalidDataException("Oversized replay record.");
                TrialReplayRecord r;
                try
                {
                    if (!line.TrimEnd().EndsWith("}", StringComparison.Ordinal)) throw new ArgumentException("Truncated JSON");
                    r = JsonUtility.FromJson<TrialReplayRecord>(line);
                }
                catch (ArgumentException)
                {
                    if (reader.Peek() == -1 && records.Count > 0 && !line.TrimEnd().EndsWith("}", StringComparison.Ordinal))
                    { warning = "Incomplete trailing record omitted."; break; }
                    throw new InvalidDataException("Corrupt JSON inside replay.");
                }
                if (r == null || r.schema != Schema || string.IsNullOrEmpty(r.kind) || r.sequence <= sequence ||
                    double.IsNaN(r.time) || double.IsInfinity(r.time) || r.time < 0 || r.time < time)
                    throw new InvalidDataException("Unsupported schema or invalid replay ordering.");
                if (records.Count == 0 && (r.kind != "header" || string.IsNullOrEmpty(r.recordingId)))
                    throw new InvalidDataException("Replay must begin with an identified header.");
                if (records.Count > 0 && (r.kind == "header" || records[records.Count - 1].kind == "end"))
                    throw new InvalidDataException("Unexpected header or records after end.");
                if (r.meshes != null) foreach (var m in r.meshes)
                {
                    if (m == null || string.IsNullOrEmpty(m.id) || !meshes.Add(m.id) || m.vertices == null || m.triangles == null ||
                        m.vertices.Length > 200000 || m.triangles.Length > 600000 || m.triangles.Length % 3 != 0)
                        throw new InvalidDataException("Invalid replay mesh.");
                    foreach (var v in m.vertices) Check(v);
                    foreach (int i in m.triangles) if (i < 0 || i >= m.vertices.Length) throw new InvalidDataException("Invalid mesh index.");
                }
                if (r.kind == "trial")
                {
                    if (string.IsNullOrEmpty(r.trialId) || !trials.Add(r.trialId) || r.objects == null || r.objects.Length == 0 || r.objects.Length > 512)
                        throw new InvalidDataException("Invalid trial snapshot.");
                    trial = r.trialId; targetId = r.targetId; ids.Clear(); int targets = 0;
                    foreach (var o in r.objects)
                    {
                        if (o == null || string.IsNullOrEmpty(o.id) || !ids.Add(o.id) || !meshes.Contains(o.mesh))
                            throw new InvalidDataException("Invalid object identity or missing mesh.");
                        Check(o.position); Check(o.scale); Check(o.rotation);
                        if (o.target) { targets++; if (o.id != r.targetId) throw new InvalidDataException("Target identity mismatch."); }
                    }
                    if (targets != 1) throw new InvalidDataException("Trial requires exactly one target.");
                }
                if (r.kind == "sample" || r.kind == "selection")
                {
                    if (trial == null || r.trialId != trial) throw new InvalidDataException("Sample/selection has no matching trial.");
                    if (r.kind == "selection" && !ids.Contains(r.objectId)) throw new InvalidDataException("Unknown selected object.");
                    if (r.kind == "selection" && r.correct != (r.objectId == targetId)) throw new InvalidDataException("Selection correctness disagrees with target.");
                    if (!string.IsNullOrEmpty(r.hoveredId) && !ids.Contains(r.hoveredId)) throw new InvalidDataException("Unknown hovered object.");
                    if (r.gazeTracked < -1 || r.gazeTracked > 1 || r.headTracked < -1 || r.headTracked > 1 ||
                        !Finite(r.dwell) || r.dwell < 0 || r.dwell > 1 || double.IsNaN(r.searchSeconds) || double.IsInfinity(r.searchSeconds) || r.searchSeconds < 0)
                        throw new InvalidDataException("Invalid tracking, dwell or search measurement.");
                    Check(r.headPosition); Check(r.headRotation); Check(r.gazeOrigin); Check(r.gazeDirection);
                    if (r.changes != null) foreach (var t in r.changes)
                    {
                        if (!ids.Contains(t.id)) throw new InvalidDataException("Unknown moved object.");
                        Check(t.position); Check(t.rotation); Check(t.scale);
                    }
                }
                if (!string.IsNullOrEmpty(r.clip)) AssetPath(info.DirectoryName, r.clip);
                records.Add(r); sequence = r.sequence; time = r.time;
                if (records.Count > MaxRecords) throw new InvalidDataException("Too many replay records.");
            }
        }
        if (records.Count == 0) throw new InvalidDataException("Empty replay.");
        if (records[records.Count - 1].kind != "end" || !records[records.Count - 1].complete)
            warning = warning ?? "Recording is incomplete; only the saved prefix is available.";
        return new TrialReplayTimeline(records) { DirectoryPath = info.DirectoryName, Warning = warning };
    }

    static void Check(Vector3 v)
    {
        if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z)) throw new InvalidDataException("Non-finite pose.");
    }
    static void Check(Quaternion q)
    {
        if (!Finite(q.x) || !Finite(q.y) || !Finite(q.z) || !Finite(q.w) || q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w < 0.5f)
            throw new InvalidDataException("Invalid quaternion.");
    }
    static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
}

/// <summary>All Unity serialization happens on the caller; only file I/O runs on the worker.</summary>
public sealed class TrialReplayWriter : IDisposable
{
    readonly BlockingCollection<Action> m_Queue = new BlockingCollection<Action>(2048);
    readonly StreamWriter m_Stream;
    readonly Thread m_Thread;
    volatile string m_Error;
    bool m_Closed;
    public string Error => m_Error;
    public TrialReplayWriter(string file)
    {
        m_Stream = new StreamWriter(new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        m_Thread = new Thread(WriteLoop) { IsBackground = true, Name = "Trial replay writer" };
        m_Thread.Start();
    }
    public bool Append(TrialReplayRecord record)
    {
        string json = JsonUtility.ToJson(record);
        return Enqueue(() => { m_Stream.WriteLine(json); });
    }
    public bool Asset(string path, byte[] bytes) => Enqueue(() =>
    {
        using (var f = new FileStream(path, FileMode.CreateNew)) f.Write(bytes, 0, bytes.Length);
    });
    bool Enqueue(Action action)
    {
        if (m_Closed || m_Error != null) return false;
        if (m_Queue.TryAdd(action)) return true;
        m_Error = "Replay write queue overflow; recording stopped without dropping evidence silently.";
        m_Queue.CompleteAdding();
        return false;
    }
    void WriteLoop()
    {
        try
        {
            int buffered = 0;
            foreach (var write in m_Queue.GetConsumingEnumerable())
            {
                write();
                if (++buffered >= 90) { m_Stream.Flush(); buffered = 0; }
            }
        }
        catch (Exception e) { m_Error = e.Message; }
        finally { try { m_Stream.Dispose(); } catch (Exception e) { m_Error = e.Message; } }
    }
    public void Dispose()
    {
        if (m_Closed) return;
        m_Closed = true;
        m_Queue.CompleteAdding();
        m_Thread.Join();
        m_Queue.Dispose();
    }
}
