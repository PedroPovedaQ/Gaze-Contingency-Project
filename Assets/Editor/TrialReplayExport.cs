using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TrialReplayExport
{
    [Serializable] public sealed class Manifest
    {
        public int schema = 1;
        public string sourceRecordingId, sourceType, sourceFile, warning;
        public double start, end;
        public int fps, frames, width, height;
        public bool complete;
        public string camera;
    }

    // Batch entry: REPLAY_INPUT, REPLAY_OUTPUT, optional REPLAY_START/END/HEAD.
    public static void Batch()
    {
        string input = Environment.GetEnvironmentVariable("REPLAY_INPUT");
        string output = Environment.GetEnvironmentVariable("REPLAY_OUTPUT");
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output)) throw new ArgumentException("Set REPLAY_INPUT and REPLAY_OUTPUT.");
        var timeline = TrialReplayFile.Read(input);
        double start = Parse("REPLAY_START", 0), end = Parse("REPLAY_END", timeline.Duration);
        Export(input, output, start, end, 30, 1280, 720, Environment.GetEnvironmentVariable("REPLAY_HEAD") == "1");
    }
    static double Parse(string name, double fallback)
    {
        string value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? fallback : double.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }
    public static void Export(string input, string output, double start, double end, int fps, int width, int height, bool head)
    {
        var timeline = TrialReplayFile.Read(input);
        if (double.IsNaN(start) || double.IsNaN(end) || start < 0 || end > timeline.Duration || end <= start || end - start > 600 || fps < 1 || fps > 120)
            throw new ArgumentException("Choose a valid range up to 10 minutes and 1–120 fps.");
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Export directory must be new.");
        if (Path.GetFullPath(output).StartsWith(Path.GetFullPath(timeline.DirectoryPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("Choose an export folder outside the source recording package.");
        if (width < 16 || height < 16 || width > 3840 || height > 2160) throw new ArgumentException("Export dimensions must be between 16x16 and 3840x2160.");
        Directory.CreateDirectory(output);
        var manifest = new Manifest { sourceRecordingId = timeline.Header.recordingId, sourceType = timeline.Header.sourceType,
            sourceFile = Path.GetFileName(input), warning = timeline.Warning, start = start, end = end, fps = fps,
            frames = (int)Math.Ceiling((end - start) * fps), width = width, height = height, camera = head ? "recorded_head" : "orbit" };
        SaveManifest(output, manifest);
        try
        {
            using (var renderer = new TrialReplayRenderer(timeline) { RecordedHead = head })
            {
                for (int i = 0; i < manifest.frames; i++)
                {
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar("Replay export", "Frame " + (i + 1) + " / " + manifest.frames, (float)i / manifest.frames))
                        throw new OperationCanceledException("Export cancelled; partial frames retained.");
                    renderer.Prepare(timeline, start + (double)i / fps);
                    var image = renderer.Capture(width, height);
                    try { File.WriteAllBytes(Path.Combine(output, "frame_" + i.ToString("D6") + ".png"), image.EncodeToPNG()); }
                    finally { UnityEngine.Object.DestroyImmediate(image); }
                }
            }
            var warnings = new List<string>();
            File.WriteAllBytes(Path.Combine(output, "audio.wav"), MixAudio(timeline, start, end, warnings));
            if (warnings.Count > 0) manifest.warning = (manifest.warning ?? "") + " " + string.Join("; ", warnings);
            manifest.complete = true; SaveManifest(output, manifest);
            Debug.Log("[TrialReplayExport] Saved " + manifest.frames + " frames to " + output);
        }
        finally { EditorUtility.ClearProgressBar(); }
    }
    static void SaveManifest(string output, Manifest m) => File.WriteAllText(Path.Combine(output, "export.json"), JsonUtility.ToJson(m, true));

    public static byte[] MixAudio(TrialReplayTimeline timeline, double start, double end, List<string> warnings)
    {
        const int rate = 48000;
        var mix = new float[(int)Math.Ceiling((end - start) * rate)];
        var records = timeline.Records;
        for (int index = 0; index < records.Count; index++)
        {
            var r = records[index];
            if (r.kind != "audio_playback_start" || r.time >= end) continue;
            if (string.IsNullOrEmpty(r.clip)) { warnings.Add("Missing audio reference at " + r.time); continue; }
            try
            {
                var samples = TrialReplayAudio.Decode(TrialReplayFile.AssetPath(timeline.DirectoryPath, r.clip), out int channels, out int sourceRate);
                double stop = r.time + (double)samples.Length / channels / sourceRate;
                for (int j = index + 1; j < records.Count; j++)
                {
                    var next = records[j];
                    if (next.kind == "audio_playback_end" || next.kind == "audio_cancelled" || next.kind == "audio_failure" ||
                        next.kind == "audio_playback_start" || next.kind == "search_paused" || next.kind == "end" || next.kind == "stop")
                    { stop = Math.Min(stop, next.time); break; }
                }
                int from = Math.Max(0, (int)Math.Ceiling((r.time - start) * rate));
                int to = Math.Min(mix.Length, (int)Math.Ceiling((stop - start) * rate));
                for (int i = from; i < to; i++)
                {
                    int sourceFrame = (int)((start + (double)i / rate - r.time) * sourceRate);
                    if (sourceFrame < 0 || sourceFrame * channels + channels > samples.Length) continue;
                    float sample = 0;
                    for (int c = 0; c < channels; c++) sample += samples[sourceFrame * channels + c];
                    mix[i] += sample / channels * r.audioVolume;
                }
            }
            catch (Exception e) { warnings.Add("Missing/unreadable " + r.clip + ": " + e.Message); }
        }
        return TrialReplayAudio.Encode(mix, 1, rate);
    }
}
