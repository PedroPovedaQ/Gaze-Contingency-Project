using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>Explicit final-summary context, joined only to the exact replay hash and trial.</summary>
public sealed class TrialReviewSummary
{
    [Serializable] sealed class Input { public string path, sha256; }
    [Serializable] sealed class Manifest { public string schema; public Input[] inputs; }
    readonly Dictionary<string, Dictionary<string, string>> m_Rows = new Dictionary<string, Dictionary<string, string>>();
    public Dictionary<string, string> For(TrialReplayRecord trial) => trial != null && m_Rows.TryGetValue(trial.trialId, out var row) ? row : null;
    public static string Hash(string file)
    {
        using (var stream = File.OpenRead(file)) using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    public TrialReviewSummary(string folder, TrialReplayTimeline timeline, string loadedHash)
    {
        string manifestPath = Path.Combine(folder, "manifest.json"), csvPath = Path.Combine(folder, "trials.csv");
        if (new FileInfo(manifestPath).Length > 16 * 1024 * 1024 || new FileInfo(csvPath).Length > 128 * 1024 * 1024)
            throw new InvalidDataException("Summary exceeds the desktop viewer size limit");
        string manifestHash = Hash(manifestPath), csvHash = Hash(csvPath);
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(folder, "manifest.json")));
        if (manifest == null || (manifest.schema != "trial-performance-subjective-v1" && manifest.schema != "trial-performance-subjective-v2") ||
            manifest.inputs == null || !manifest.inputs.Any(x => x.sha256 == loadedHash))
            throw new InvalidDataException("Export does not reference this exact recording. Generate a matching trial export first.");
        var rows = Parse(File.ReadAllText(Path.Combine(folder, "trials.csv")));
        if (rows.Count == 0) throw new InvalidDataException("Empty summary CSV");
        var headers = rows[0];
        if (headers.Distinct().Count() != headers.Count) throw new InvalidDataException("Duplicate CSV headers");
        foreach (var required in new[] { "replay_recording_id", "trial_id", "participant_id", "voice_condition", "is_practice" })
            if (!headers.Contains(required)) throw new InvalidDataException("Missing CSV column: " + required);
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].Count != headers.Count) throw new InvalidDataException("Malformed CSV row " + (i + 1));
            var row = headers.Select((h, j) => new { h, value = rows[i][j] }).ToDictionary(x => x.h, x => x.value);
            if (row["replay_recording_id"] != timeline.Header.recordingId) continue;
            var trial = timeline.Trials.Find(t => (t.practice ? timeline.Header.recordingId + ":" + t.trialId : t.sourceTrialId) == row["trial_id"]);
            if (trial == null) throw new InvalidDataException("Summary names an unknown replay trial");
            Check(row, "participant_id", trial.participantCode ?? timeline.Header.participantCode);
            Check(row, "voice_condition", trial.voice);
            Check(row, "is_practice", trial.practice ? "1" : "0");
            if (!trial.practice && trial.runNumber > 0) Check(row, "run_number", trial.runNumber.ToString());
            if (!trial.practice) Check(row, "block_index", trial.block.ToString());
            if (m_Rows.ContainsKey(trial.trialId)) throw new InvalidDataException("Duplicate trial summary identity");
            m_Rows.Add(trial.trialId, row);
        }
        if (manifestHash != Hash(manifestPath) || csvHash != Hash(csvPath))
            throw new IOException("Summary changed while opening. Use a copied, completed export.");
        if (m_Rows.Count == 0) throw new InvalidDataException("No matching trial summaries");
    }
    static void Check(Dictionary<string, string> row, string key, string expected)
    {
        if (!string.IsNullOrEmpty(expected) && row.TryGetValue(key, out var actual) && !string.IsNullOrEmpty(actual) && actual != expected)
            throw new InvalidDataException("Summary mismatch: " + key);
    }
    // RFC-style quoted fields including escaped quotes and embedded line breaks.
    public static List<List<string>> Parse(string text)
    {
        var result = new List<List<string>>(); var row = new List<string>(); var field = new StringBuilder();
        bool quoted = false, closed = false;
        text = text.TrimStart('\ufeff');
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } }
                else field.Append(c);
            }
            else if (c == ',' || c == '\n' || c == '\r')
            {
                row.Add(field.ToString()); field.Clear(); closed = false;
                if (c != ',') { result.Add(row); row = new List<string>(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
            }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else { if (closed || c == '"') throw new InvalidDataException("Invalid CSV quoting"); field.Append(c); }
        }
        if (quoted) throw new InvalidDataException("Unclosed CSV quoted field");
        if (row.Count > 0 || field.Length > 0 || closed) { row.Add(field.ToString()); result.Add(row); }
        return result;
    }
}
