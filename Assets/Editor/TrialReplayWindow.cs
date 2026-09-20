using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class TrialReplayWindow : EditorWindow
{
    TrialReplayTimeline m_Timeline;
    TrialReplayRenderer m_Renderer;
    TrialReplayRehearsal m_Rehearsal;
    TrialReplayRecord m_RehearsalTrial;
    double m_Time, m_LastUpdate;
    bool m_Playing;
    float m_Speed = 1;
    string m_Path, m_Message;
    readonly Dictionary<string, AudioClip> m_Clips = new Dictionary<string, AudioClip>();
    TrialReplayRecord m_PlayingAudio;
    static readonly Type s_AudioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

    [MenuItem("Tools/Codex/Trial Replay/Open Viewer")]
    public static void Open() => GetWindow<TrialReplayWindow>("Trial Replay");
    [MenuItem("Tools/Codex/Trial Replay/Create Synthetic Demo")]
    public static void Demo()
    {
        string path = TrialReplayChecks.CreateDemo(Path.Combine(Application.temporaryCachePath, "TrialReplayDemo_" + Guid.NewGuid().ToString("N")));
        GetWindow<TrialReplayWindow>("Trial Replay").Load(path);
    }
    void OnEnable() { minSize = new Vector2(650, 480); m_LastUpdate = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    void OnDisable()
    {
        EditorApplication.update -= Tick;
        m_Rehearsal?.Dispose(); m_Rehearsal = null;
        StopAudio(); m_Renderer?.Dispose(); m_Renderer = null;
        foreach (var clip in m_Clips.Values) DestroyImmediate(clip);
        m_Clips.Clear();
    }
    public void Load(string path)
    {
        if (m_Rehearsal != null) throw new InvalidOperationException("Finish rehearsal before loading another recording.");
        var timeline = TrialReplayFile.Read(path);
        var renderer = new TrialReplayRenderer(timeline);
        StopAudio(); m_Renderer?.Dispose();
        foreach (var clip in m_Clips.Values) DestroyImmediate(clip);
        m_Clips.Clear();
        m_Timeline = timeline; m_Renderer = renderer; m_Path = path; m_Time = 0; m_Playing = false;
        m_Message = timeline.Warning; Seek(0); Repaint();
    }
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup, delta = now - m_LastUpdate; m_LastUpdate = now;
        if (m_Playing && m_Timeline != null)
        {
            m_Time = Math.Min(m_Timeline.Duration, m_Time + delta * m_Speed);
            m_Renderer.Prepare(m_Timeline, m_Time); SyncAudio(false);
            if (m_Time >= m_Timeline.Duration) { m_Playing = false; StopAudio(); }
            Repaint();
        }
    }
    void Seek(double time)
    {
        m_Time = time; m_Renderer.Prepare(m_Timeline, m_Time); SyncAudio(true); Repaint();
    }
    void StopAudio()
    {
        s_AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        m_PlayingAudio = null;
    }
    void SyncAudio(bool force)
    {
        var r = m_Timeline.State.Audio;
        if (!m_Playing || m_Speed != 1 || r == null || string.IsNullOrEmpty(r.clip)) { if (m_PlayingAudio != null) StopAudio(); return; }
        if (!force && m_PlayingAudio == r) return;
        StopAudio(); m_PlayingAudio = r;
        try
        {
            if (!m_Clips.TryGetValue(r.clip, out var clip))
            {
                var samples = TrialReplayAudio.Decode(TrialReplayFile.AssetPath(m_Timeline.DirectoryPath, r.clip), out int channels, out int rate);
                for (int i = 0; i < samples.Length; i++) samples[i] *= r.audioVolume;
                clip = AudioClip.Create(r.clip, samples.Length / channels, channels, rate, false); clip.SetData(samples, 0);
                m_Clips.Add(r.clip, clip);
            }
            int offset = (int)((m_Time - r.time) * clip.frequency);
            if (offset >= clip.samples) return;
            var method = s_AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            if (method == null) { m_Message = "Audio preview unavailable in this Editor. WAV export still preserves audio."; return; }
            method.Invoke(null, new object[] { clip, Math.Max(0, offset), false });
        }
        catch (Exception e) { m_Message = "Audio unavailable: " + e.Message; }
    }
    void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { m_Message = e.Message; Debug.LogWarning("[TrialReplay] " + e.Message); }
    }
    void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            using (new EditorGUI.DisabledScope(m_Rehearsal != null))
            {
                if (GUILayout.Button("Open recording", EditorStyles.toolbarButton)) Guard(() =>
                {
                    string path = EditorUtility.OpenFilePanel("Open trial recording", Application.persistentDataPath, "jsonl");
                    if (!string.IsNullOrEmpty(path)) Load(path);
                });
                if (GUILayout.Button("Synthetic demo", EditorStyles.toolbarButton)) Guard(Demo);
            }
            GUILayout.FlexibleSpace(); GUILayout.Label("TRIAL REPLAY", EditorStyles.miniBoldLabel);
        }
        if (m_Timeline == null) { EditorGUILayout.HelpBox("Open recording.jsonl or create a synthetic demo. Playback does not run the study scene.", MessageType.Info); return; }
        EditorGUILayout.LabelField(m_Timeline.Header.sourceType + "  •  " + m_Timeline.Header.recordingId, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(m_Timeline.Warning)) EditorGUILayout.HelpBox(m_Timeline.Warning, MessageType.Warning);
        if (!string.IsNullOrEmpty(m_Message)) EditorGUILayout.HelpBox(m_Message, MessageType.Info);
        using (new EditorGUI.DisabledScope(m_Rehearsal != null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(m_Playing ? "Pause" : "Play", GUILayout.Width(65))) { m_Playing = !m_Playing; SyncAudio(true); }
                if (GUILayout.Button("|<", GUILayout.Width(35))) Seek(0);
                if (GUILayout.Button("Next event", GUILayout.Width(90)))
                {
                    var next = m_Timeline.Records.Find(r => r.time > m_Time + 0.000001 && (r.kind == "selection" || r.kind == "audio_playback_start"));
                    if (next != null) Seek(next.time);
                }
                float speed = EditorGUILayout.Popup(Array.IndexOf(new[] { 0.25f, 0.5f, 1f, 2f }, m_Speed), new[] { "0.25×", "0.5×", "1×", "2×" }, GUILayout.Width(70));
                float nextSpeed = new[] { 0.25f, 0.5f, 1f, 2f }[(int)speed];
                if (nextSpeed != m_Speed) { m_Speed = nextSpeed; SyncAudio(true); }
                var names = m_Timeline.Trials.ConvertAll(t => t.trialId + (t.practice ? " (practice)" : "")).ToArray();
                int current = Math.Max(0, m_Timeline.Trials.FindIndex(t => t == m_Timeline.State.Trial));
                int chosen = EditorGUILayout.Popup(current, names);
                if (chosen != current) Seek(m_Timeline.Trials[chosen].time);
            }
            float nextTime = EditorGUILayout.Slider((float)m_Time, 0, (float)m_Timeline.Duration);
            if (Math.Abs(nextTime - m_Time) > 0.00001) Seek(nextTime);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            int camera = m_Renderer.RecordedHead ? 1 : m_Renderer.Overhead ? 2 : 0;
            camera = GUILayout.Toolbar(camera, new[] { "Orbit", "Recorded head", "Overhead" });
            m_Renderer.RecordedHead = camera == 1; m_Renderer.Overhead = camera == 2;
            m_Renderer.ShowGaze = GUILayout.Toggle(m_Renderer.ShowGaze, "Gaze");
            m_Renderer.ShowTarget = GUILayout.Toggle(m_Renderer.ShowTarget, "Target");
        }
        if (m_Speed != 1) GUILayout.Label("Audio muted at non-1× speed.", EditorStyles.miniLabel);
        Rect rect = GUILayoutUtility.GetRect(200, 10000, 220, 10000, GUILayout.ExpandHeight(true));
        m_Renderer.Prepare(m_Timeline, m_Time, m_Rehearsal != null);
        if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, m_Renderer.Render(rect), ScaleMode.StretchToFill, false);
        HandleViewport(rect);
        var state = m_Timeline.State;
        string status = state.Trial == null ? "Waiting for first trial" : state.Trial.trialId + "  target: " + state.Trial.targetId;
        GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, 22), status, EditorStyles.whiteBoldLabel);
        if (state.Sample != null)
            GUI.Label(new Rect(rect.x + 12, rect.y + 34, rect.width - 24, 22),
                $"Search {state.Sample.searchSeconds:F2}s   Dwell {state.Sample.dwell:P0}   Gaze tracked: {state.Sample.gazeTracked} (-1 unknown)", EditorStyles.whiteLabel);
        if (state.Selection != null && m_Rehearsal == null)
            GUI.Label(new Rect(rect.x + 12, rect.yMax - 28, rect.width - 24, 22),
                $"Recorded choice: {state.Selection.objectId} — {(state.Selection.correct ? "correct" : "wrong")}", EditorStyles.whiteLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(m_Rehearsal != null || state.Trial == null))
            {
                if (GUILayout.Button("Repeat layout (mouse rehearsal)")) Guard(() =>
                {
                    m_Playing = false; StopAudio(); m_RehearsalTrial = state.Trial;
                    Seek(state.Trial.time); m_Renderer.RecordedHead = false; m_Renderer.Overhead = false;
                    m_Rehearsal = new TrialReplayRehearsal(m_Timeline, m_RehearsalTrial,
                        Path.Combine(Application.persistentDataPath, "GazeReplayDerived"), EditorApplication.timeSinceStartup);
                    m_Message = "NEW DESKTOP RESPONSES • Click the target. Right-drag to orbit; scroll to zoom. Target: " +
                        Array.Find(m_RehearsalTrial.objects, o => o.target).color + " " + Array.Find(m_RehearsalTrial.objects, o => o.target).shape;
                });
                if (GUILayout.Button("Export frames + audio")) Guard(() =>
                {
                    string parent = EditorUtility.OpenFolderPanel("Choose export parent (new subfolder created)", "", "");
                    if (string.IsNullOrEmpty(parent)) return;
                    m_Playing = false; StopAudio();
                    string output = Path.Combine(parent, "replay_export_" + Guid.NewGuid().ToString("N"));
                    TrialReplayExport.Export(m_Path, output, 0, m_Timeline.Duration, 30, 1280, 720, m_Renderer.RecordedHead);
                    m_Message = "Export saved: " + output + ". Encode with scripts/encode-trial-replay.py.";
                });
            }
            if (m_Rehearsal != null && GUILayout.Button("End rehearsal")) EndRehearsal(false);
        }
    }
    void HandleViewport(Rect rect)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;
        if (e.type == EventType.ScrollWheel) { m_Renderer.Distance = Mathf.Clamp(m_Renderer.Distance + e.delta.y * 0.1f, 0.3f, 20); e.Use(); Repaint(); }
        if (e.type == EventType.MouseDrag && e.button == 1)
        { m_Renderer.Yaw += e.delta.x; m_Renderer.Pitch = Mathf.Clamp(m_Renderer.Pitch + e.delta.y, -85, 85); e.Use(); Repaint(); }
        if (e.type == EventType.MouseDown && e.button == 0 && m_Rehearsal != null)
        {
            var uv = new Vector2((e.mousePosition.x - rect.x) / rect.width, 1 - (e.mousePosition.y - rect.y) / rect.height);
            string id = m_Renderer.Pick(uv);
            if (id != null) Guard(() => { if (m_Rehearsal.Select(id, EditorApplication.timeSinceStartup)) EndRehearsal(true); else m_Message = "Wrong choice: " + id + ". Keep searching."; });
            e.Use();
        }
    }
    void EndRehearsal(bool correct)
    {
        Guard(() =>
        {
            m_Rehearsal.Close(EditorApplication.timeSinceStartup, correct);
            m_Message = (correct ? "Correct. " : "Cancelled. ") + "Desktop responses saved: " + m_Rehearsal.DirectoryPath;
            if (m_Rehearsal.Error != null) m_Message = "Rehearsal incomplete: " + m_Rehearsal.Error;
            m_Rehearsal.Dispose(); m_Rehearsal = null;
        });
    }
}
