using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Incremental UTF-8 JSONL reader. Incomplete tails wait for bytes, never become fabricated samples.</summary>
public sealed class TrialReviewLiveStream : IDisposable
{
    readonly FileStream m_File;
    readonly MemoryStream m_Line = new MemoryStream();
    readonly byte[] m_Buffer = new byte[65536];
    readonly TrialReplayFile.Validator m_Validator;
    readonly UTF8Encoding m_Utf8 = new UTF8Encoding(false, true);
    int m_Position, m_Length;
    public TrialReplayTimeline Timeline { get; }
    public bool Complete => Timeline.Records.Count > 0 && Timeline.Records[Timeline.Records.Count - 1].kind == "end";
    public bool CaughtUp => m_Position == m_Length && m_File.Position == m_File.Length;
    public bool PartialTail => m_Line.Length != 0;

    public TrialReviewLiveStream(string path)
    {
        path = Path.GetFullPath(path);
        m_File = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        m_Validator = new TrialReplayFile.Validator(Path.GetDirectoryName(path));
        Timeline = new TrialReplayTimeline(new List<TrialReplayRecord>())
            { FilePath = path, DirectoryPath = Path.GetDirectoryName(path) };
    }

    public int Poll(int maxRecords = 180, double budgetMilliseconds = 8)
    {
        if (m_File.Length > 1024L * 1024 * 1024) throw new InvalidDataException("Live viewer limit: 1 GiB per replay. Wireless archiving continues.");
        if (m_File.Length < m_File.Position) throw new InvalidDataException("Live source shrank; reopen the preserved recording.");
        int count = 0;
        var watch = Stopwatch.StartNew();
        while (count < maxRecords && watch.Elapsed.TotalMilliseconds < budgetMilliseconds)
        {
            if (m_Position == m_Length)
            {
                m_Length = m_File.Read(m_Buffer, 0, m_Buffer.Length); m_Position = 0;
                if (m_Length == 0) break;
            }
            byte next = m_Buffer[m_Position++];
            if (next != 10)
            {
                m_Line.WriteByte(next);
                if (m_Line.Length > 16000000) throw new InvalidDataException("Oversized live replay record.");
                continue;
            }
            string line = m_Utf8.GetString(m_Line.ToArray()); m_Line.SetLength(0);
            if (!line.TrimEnd().EndsWith("}", StringComparison.Ordinal)) throw new InvalidDataException("Malformed complete live replay line.");
            var record = JsonUtility.FromJson<TrialReplayRecord>(line);
            m_Validator.Accept(record);
            Timeline.AppendValidated(record); count++;
        }
        return count;
    }

    public void Dispose() { m_File.Dispose(); m_Line.Dispose(); }
}
