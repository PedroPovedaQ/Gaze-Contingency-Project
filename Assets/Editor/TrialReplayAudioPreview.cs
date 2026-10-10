using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

// Reconstruct software playback intervals, never participant acoustic onset.
// Speech has one channel; recorded one-shot effects may overlap that channel.
public sealed class TrialReplayAudioPlan
{
    public sealed class Segment
    {
        public TrialReplayRecord Start;
        public string Path;
        public double End;
        public double FadeStart = double.PositiveInfinity, FadeDuration;
        public float GainAt(double time) => time < FadeStart || FadeDuration <= 0 ? 1f :
            (float)Math.Max(0, 1 - (time - FadeStart) / FadeDuration);
        public long EndSequence = long.MaxValue;
        public bool Contains(double time, long sequence) =>
            (time > Start.time || time == Start.time && sequence >= Start.sequence) &&
            (time < End || time == End && sequence < EndSequence);
        public void Stop(TrialReplayRecord record)
        {
            if (record.time <= End) { End = record.time; EndSequence = record.sequence; }
        }
    }
    public readonly List<Segment> Segments = new List<Segment>();
    public readonly List<string> Warnings = new List<string>();

    public TrialReplayAudioPlan(TrialReplayTimeline timeline)
    {
        var durations = new Dictionary<string, double>();
        Segment speech = null;
        foreach (var record in timeline.Records)
        {
            if (record.kind == "audio_playback_start")
            {
                bool effect = (record.detail ?? "").Contains("software_one_shot_start");
                if (!effect) { speech?.Stop(record); speech = null; }
                try
                {
                    if (string.IsNullOrEmpty(record.clip)) throw new InvalidDataException("No saved audio asset.");
                    string path = TrialReplayFile.AssetPath(timeline.DirectoryPath, record.clip);
                    if (!durations.TryGetValue(path, out double duration))
                    {
                        var info = TrialReplayAudio.Inspect(path);
                        duration = (double)info.Frames / info.Rate;
                        durations.Add(path, duration);
                    }
                    var segment = new Segment { Start = record, Path = path, End = record.time + duration };
                    Segments.Add(segment);
                    if (!effect) speech = segment;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
                { Warnings.Add("Audio sequence " + record.sequence + ": " + error.Message); }
            }
            else if (record.kind == "audio_fade_start" && speech != null)
            {
                foreach (string field in (record.detail ?? "").Split(';'))
                    if (field.StartsWith("fade_seconds=", StringComparison.Ordinal) &&
                        double.TryParse(field.Substring(13), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double duration) && duration > 0 && duration <= 1)
                    { speech.FadeStart = record.time; speech.FadeDuration = duration; break; }
            }
            else if (record.kind == "audio_playback_end" || record.kind == "audio_cancelled" || record.kind == "audio_failure")
            { speech?.Stop(record); speech = null; }
            else if (record.kind == "stop" || record.kind == "end")
            {
                foreach (var segment in Segments) segment.Stop(record);
                speech = null;
            }
        }
    }
}

// Own only this window's preview clips. Never stop other Editor audio previews.
public sealed class TrialReplayAudioPreview : IDisposable
{
    public interface IOutput
    {
        object Play(float[] samples, int channels, int rate, int offsetFrames, string name);
        void Stop(object handle);
    }
    public sealed class EditorOutput : IOutput, IDisposable
    {
        Scene m_Scene;
        readonly HashSet<AudioSource> m_Sources = new HashSet<AudioSource>();
        public EditorOutput()
        {
            m_Scene = EditorSceneManager.NewPreviewScene();
            // Two-dimensional replay audio can use an existing scene listener;
            // leave its transform/settings untouched and avoid duplicate listeners.
            bool listenerAvailable = false;
            foreach (var existing in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (existing.isActiveAndEnabled) { listenerAvailable = true; break; }
            if (!listenerAvailable)
            {
                var listener = new GameObject("Replay audio listener") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(listener, m_Scene);
                listener.AddComponent<AudioListener>();
            }
        }
        public object Play(float[] samples, int channels, int rate, int offsetFrames, string name)
        {
            var clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, m_Scene);
            try
            {
                if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Cannot populate preview audio.");
                var source = go.AddComponent<AudioSource>();
                source.clip = clip; source.spatialBlend = 0; source.playOnAwake = false;
                source.Play(); source.timeSamples = offsetFrames;
                m_Sources.Add(source); return source;
            }
            catch { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(clip); throw; }
        }
        public void Stop(object handle)
        {
            var source = (AudioSource)handle;
            if (!m_Sources.Remove(source)) return;
            var clip = source.clip; source.Stop();
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            UnityEngine.Object.DestroyImmediate(clip);
        }
        public void Dispose()
        {
            foreach (var source in new List<AudioSource>(m_Sources)) Stop(source);
            if (m_Scene.IsValid()) EditorSceneManager.ClosePreviewScene(m_Scene);
        }
    }
    public readonly TrialReplayAudioPlan Plan;
    public string Warning { get; private set; }
    readonly IOutput m_Output;
    readonly Dictionary<TrialReplayAudioPlan.Segment, object> m_Active = new Dictionary<TrialReplayAudioPlan.Segment, object>();
    readonly HashSet<TrialReplayAudioPlan.Segment> m_Failed = new HashSet<TrialReplayAudioPlan.Segment>();
    public int ActiveCount => m_Active.Count;

    public TrialReplayAudioPreview(TrialReplayTimeline timeline, IOutput output = null)
    {
        Plan = new TrialReplayAudioPlan(timeline); m_Output = output ?? new EditorOutput();
        if (Plan.Warnings.Count > 0) Warning = string.Join("\n", Plan.Warnings);
    }

    // Reconcile new live events/assets without restarting already playing clips.
    public void Refresh(TrialReplayTimeline timeline)
    {
        var updated = new TrialReplayAudioPlan(timeline);
        var existing = new Dictionary<long, TrialReplayAudioPlan.Segment>();
        foreach (var segment in Plan.Segments) existing[segment.Start.sequence] = segment;
        var retained = new HashSet<TrialReplayAudioPlan.Segment>();
        Plan.Segments.Clear();
        foreach (var segment in updated.Segments)
        {
            if (existing.TryGetValue(segment.Start.sequence, out var previous))
            {
                if ((previous.FadeStart != segment.FadeStart || previous.FadeDuration != segment.FadeDuration) && m_Active.ContainsKey(previous))
                    Stop(previous); // Reconstruct the new envelope at the current cursor on the next Sync.
                previous.End = segment.End; previous.EndSequence = segment.EndSequence;
                previous.FadeStart = segment.FadeStart; previous.FadeDuration = segment.FadeDuration;
                retained.Add(previous); Plan.Segments.Add(previous);
            }
            else Plan.Segments.Add(segment);
        }
        foreach (var active in new List<TrialReplayAudioPlan.Segment>(m_Active.Keys))
            if (!retained.Contains(active)) Stop(active);
        m_Failed.Clear();
        Plan.Warnings.Clear(); Plan.Warnings.AddRange(updated.Warnings);
        Warning = Plan.Warnings.Count == 0 ? null : string.Join("\n", Plan.Warnings);
    }

    public void Sync(double time, bool playing, float speed, long sequence = long.MaxValue, bool force = false)
    {
        if (force || !playing || speed != 1) Stop();
        if (!playing || speed != 1) return;
        foreach (var segment in new List<TrialReplayAudioPlan.Segment>(m_Active.Keys))
            if (!segment.Contains(time, sequence)) Stop(segment);
        foreach (var segment in Plan.Segments)
        {
            if (!segment.Contains(time, sequence) || m_Active.ContainsKey(segment) || m_Failed.Contains(segment)) continue;
            try
            {
                var samples = TrialReplayAudio.Decode(segment.Path, out int channels, out int rate);
                // Cut at the recorded stop even if the next Editor update is late.
                int frames = (int)Math.Min(samples.Length / channels, Math.Floor((segment.End - segment.Start.time) * rate));
                int offset = (int)Math.Floor(Math.Max(0, time - segment.Start.time) * rate);
                if (frames <= offset) continue;
                Array.Resize(ref samples, frames * channels);
                for (int i = 0; i < samples.Length; i++)
                    samples[i] *= segment.Start.audioVolume * segment.GainAt(segment.Start.time + (double)(i / channels) / rate);
                var handle = m_Output.Play(samples, channels, rate, offset, "Replay " + segment.Start.sequence);
                m_Active.Add(segment, handle);
            }
            catch (Exception error)
            {
                m_Failed.Add(segment);
                Warning = "Audio sequence " + segment.Start.sequence + " unavailable: " + error.Message;
            }
        }
    }
    void Stop(TrialReplayAudioPlan.Segment segment)
    {
        var handle = m_Active[segment]; m_Active.Remove(segment);
        try { m_Output.Stop(handle); }
        catch (Exception error) { Warning = "Could not stop preview: " + error.Message; }
    }
    public void Stop()
    {
        foreach (var segment in new List<TrialReplayAudioPlan.Segment>(m_Active.Keys)) Stop(segment);
    }
    public void Dispose() { Stop(); (m_Output as IDisposable)?.Dispose(); }
}
