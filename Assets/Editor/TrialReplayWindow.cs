using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class TrialReplayWindow : EditorWindow
{
    TrialReplayTimeline m_Timeline;
    TrialReplayRenderer m_Renderer;
    double m_Time, m_LastUpdate;
    bool m_Playing;
    long m_SequenceCursor = long.MaxValue;
    bool m_ShowInspector;
    Vector2 m_InspectorScroll;
    float m_Speed = 1;
    string m_Message;
    TrialReplayAudioPreview m_Audio;

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
        StopAudio(); m_Renderer?.Dispose(); m_Renderer = null;
        m_Audio?.Dispose(); m_Audio = null;
    }
    public void Load(string path)
    {
        var timeline = TrialReplayFile.Read(path);
        var renderer = new TrialReplayRenderer(timeline);
        StopAudio(); m_Renderer?.Dispose();
        m_Audio?.Dispose(); m_Audio = null;
        m_Audio = new TrialReplayAudioPreview(timeline);
        m_Timeline = timeline; m_Renderer = renderer; m_Time = 0; m_Playing = false; m_SequenceCursor = long.MaxValue;
        m_Message = timeline.Warning; Seek(0); Repaint();
    }
    void Tick()
    {
        double now = EditorApplication.timeSinceStartup, delta = now - m_LastUpdate; m_LastUpdate = now;
        if (m_Playing && m_Timeline != null)
        {
            m_Time = Math.Min(m_Timeline.Duration, m_Time + delta * m_Speed);
            m_SequenceCursor = long.MaxValue;
            m_Renderer.Prepare(m_Timeline, m_Time); SyncAudio(false);
            if (m_Time >= m_Timeline.Duration) { m_Playing = false; StopAudio(); }
            Repaint();
        }
    }
    void Seek(double time, long sequence = long.MaxValue)
    {
        m_Time = time; m_SequenceCursor = sequence;
        m_Renderer.Prepare(m_Timeline, m_Time, maxSequence: m_SequenceCursor); SyncAudio(true); Repaint();
    }
    void StepSample(int direction)
    {
        m_Playing = false; StopAudio();
        TrialReplayRecord chosen = null;
        long cursor = m_SequenceCursor == long.MaxValue ? m_Timeline.CurrentRecord?.sequence ?? -1 : m_SequenceCursor;
        foreach (var record in m_Timeline.Records)
        {
            if (record.kind != "sample" && !(record.kind == "selection" && record.schema >= 2)) continue;
            if (direction > 0 && record.sequence > cursor) { chosen = record; break; }
            if (direction < 0 && (record.time < m_Time && record.sequence == cursor || record.sequence < cursor)) chosen = record;
        }
        if (chosen != null) Seek(chosen.time, chosen.sequence);
    }

    [Serializable]
    public sealed class CursorSnapshot
    {
        public string schema = "unity-replay-cursor-v1", sourceFile;
        public double recordingSeconds, maxSampleAgeSeconds, sampleAgeSeconds;
        public bool sampleCurrent;
        public int sampleSourceLine, currentSourceLine;
        public long selectedSequence;
        public TrialReplayRecord header, trial, sample, currentRecord;
        public TrialReplayTransform[] transforms;
    }

    public static CursorSnapshot Snapshot(TrialReplayTimeline timeline, double time, double maxAge, long sequence = long.MaxValue)
    {
        timeline.Seek(time, sequence);
        var state = timeline.State;
        return new CursorSnapshot { sourceFile=timeline.FilePath, recordingSeconds=time,
            maxSampleAgeSeconds=maxAge, sampleAgeSeconds=state.Sample == null ? -1 : time-state.Sample.time,
            sampleCurrent=state.HasCurrentSample(time,maxAge), sampleSourceLine=timeline.SampleSourceLine,
            currentSourceLine=timeline.CurrentSourceLine, selectedSequence=sequence == long.MaxValue ? -1 : sequence,
            header=timeline.Header, trial=state.Trial, sample=state.Sample, currentRecord=timeline.CurrentRecord,
            transforms=new List<TrialReplayTransform>(state.Transforms.Values).ToArray() };
    }

    void SaveSnapshot()
    {
        string path = EditorUtility.SaveFilePanel("Save replay cursor snapshot", "", "replay-cursor-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss"), "json");
        if (string.IsNullOrEmpty(path)) return;
        var snapshot = Snapshot(m_Timeline,m_Time,m_Renderer.MaxSampleAgeSeconds,m_SequenceCursor);
        using (var stream = new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write)))
            stream.Write(JsonUtility.ToJson(snapshot,true));
        m_Message = "Saved cursor snapshot. Source recording remains unchanged. Negative age/sequence sentinels mean unavailable/not selected.";
    }
    void StopAudio() => m_Audio?.Stop();
    void SyncAudio(bool force)
    {
        m_Audio?.Sync(m_Time, m_Playing, m_Speed, m_SequenceCursor, force);
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
            if (GUILayout.Button("Open recording", EditorStyles.toolbarButton)) Guard(() =>
            {
                string path = EditorUtility.OpenFilePanel("Open trial recording", Application.persistentDataPath, "jsonl");
                if (!string.IsNullOrEmpty(path)) Load(path);
            });
            if (GUILayout.Button("Synthetic demo", EditorStyles.toolbarButton)) Guard(Demo);
            GUILayout.FlexibleSpace(); GUILayout.Label("TRIAL REPLAY", EditorStyles.miniBoldLabel);
        }
        if (m_Timeline == null) { EditorGUILayout.HelpBox("Open recording.jsonl or create a synthetic demo. Playback does not run the study scene.", MessageType.Info); return; }
        EditorGUILayout.LabelField(m_Timeline.Header.sourceType + "  •  " + m_Timeline.Header.recordingId, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(m_Timeline.Warning)) EditorGUILayout.HelpBox(m_Timeline.Warning, MessageType.Warning);
        if (!string.IsNullOrEmpty(m_Message)) EditorGUILayout.HelpBox(m_Message, MessageType.Info);
        if (!string.IsNullOrEmpty(m_Audio?.Warning)) EditorGUILayout.HelpBox(m_Audio.Warning, MessageType.Warning);
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
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Previous sample")) StepSample(-1);
            if (GUILayout.Button("Next sample")) StepSample(1);
            if (GUILayout.Button("Save cursor snapshot")) Guard(SaveSnapshot);
            EditorGUILayout.LabelField("Max pose age (s)",GUILayout.Width(115));
            double age=EditorGUILayout.DoubleField(m_Renderer.MaxSampleAgeSeconds,GUILayout.Width(65));
            if (!double.IsNaN(age) && !double.IsInfinity(age) && age>0) m_Renderer.MaxSampleAgeSeconds=age;
        }
        EditorGUI.BeginChangeCheck();
        float nextTime = EditorGUILayout.Slider((float)m_Time, 0, (float)m_Timeline.Duration);
        if (EditorGUI.EndChangeCheck()) { m_Playing=false; Seek(nextTime); }
        using (new EditorGUILayout.HorizontalScope())
        {
            int camera = m_Renderer.RecordedHead ? 1 : m_Renderer.Overhead ? 2 : 0;
            camera = GUILayout.Toolbar(camera, new[] { "Orbit", "Recorded head", "Overhead" });
            m_Renderer.RecordedHead = camera == 1; m_Renderer.Overhead = camera == 2;
            m_Renderer.ShowGaze = GUILayout.Toggle(m_Renderer.ShowGaze, "Gaze");
            m_Renderer.ShowControllers = GUILayout.Toggle(m_Renderer.ShowControllers, "Controllers");
            m_Renderer.ShowTarget = GUILayout.Toggle(m_Renderer.ShowTarget, "Target");
        }
        if (m_Speed != 1) GUILayout.Label("Audio muted at non-1× speed.", EditorStyles.miniLabel);
        Rect rect = GUILayoutUtility.GetRect(200, 10000, 220, 10000, GUILayout.ExpandHeight(true));
        m_Renderer.Prepare(m_Timeline, m_Time, maxSequence: m_SequenceCursor);
        if (Event.current.type == EventType.Repaint) GUI.DrawTexture(rect, m_Renderer.Render(rect), ScaleMode.StretchToFill, false);
        HandleViewport(rect);
        var state = m_Timeline.State;
        string status = state.Trial == null ? "Waiting for first trial" : state.Trial.trialId + "  target: " + state.Trial.targetId;
        GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, 22), status, EditorStyles.whiteBoldLabel);
        if (state.Sample != null)
            GUI.Label(new Rect(rect.x + 12, rect.y + 34, rect.width - 24, 22),
                m_Renderer.SampleCurrent ?
                $"Search {state.Sample.searchSeconds:F2}s   Gaze tracked: {state.Sample.gazeTracked}   Trigger L/R: {state.Sample.leftController?.triggerHeld ?? -1}/{state.Sample.rightController?.triggerHeld ?? -1} (-1 unknown)" :
                $"No current pose · last sample {m_Time-state.Sample.time:F3}s ago · historical values in inspector", EditorStyles.whiteLabel);
        if (m_Renderer.RecordedHead && !m_Renderer.UsingRecordedHead)
            GUI.Label(new Rect(rect.x+12,rect.y+58,rect.width-24,22),"Recorded head unavailable · orbit overview shown",EditorStyles.whiteLabel);
        if (state.Selection != null)
            GUI.Label(new Rect(rect.x + 12, rect.yMax - 28, rect.width - 24, 22),
                $"Recorded choice: {state.Selection.objectId} — {(state.Selection.correct ? "correct" : "wrong")}", EditorStyles.whiteLabel);
        if (state.Trial != null)
            EditorGUILayout.LabelField($"Voice: {state.Trial.voice}   Block: {state.Trial.block + 1}   Start wall: {state.Trial.startPlane * 45}°   |θ|: {state.Trial.absoluteTheta}°");
        if (state.Readiness != null && state.Readiness.readinessActive)
            EditorGUILayout.LabelField($"Readiness cross: {state.Readiness.readinessProgress:P0}   Announcement ready: {state.Readiness.announcementReady}");
        if (m_Timeline.Header.schema == 1)
            EditorGUILayout.HelpBox("Legacy dwell recording: controller telemetry is unavailable.", MessageType.Info);
        m_ShowInspector=EditorGUILayout.Foldout(m_ShowInspector,"Source samples and cursor state (no interpolation)",true);
        if (m_ShowInspector)
        {
            m_InspectorScroll=EditorGUILayout.BeginScrollView(m_InspectorScroll,GUILayout.Height(190));
            EditorGUILayout.LabelField($"Recording {m_Time:F6}s · record line {m_Timeline.CurrentSourceLine} · sample line {m_Timeline.SampleSourceLine}");
            EditorGUILayout.LabelField($"Latest record: {m_Timeline.CurrentRecord?.kind ?? "none"} · searching: {state.Searching} · visible: {state.Visible}");
            EditorGUILayout.LabelField($"Sample current: {m_Renderer.SampleCurrent} · frame {state.Sample?.frame} · sequence {state.Sample?.sequence}");
            EditorGUILayout.LabelField($"Raw gaze: {state.Sample?.gazeRecordingId ?? "unknown"} / {state.Sample?.gazeObservationSequence ?? -1}");
            EditorGUILayout.LabelField($"Head tracked: {state.Sample?.headTracked ?? -1} · gaze tracked: {state.Sample?.gazeTracked ?? -1} (-1 unknown)");
            EditorGUILayout.LabelField($"Gaze delivery age at sample: {state.Sample?.gazeDeliveryAge:F6}s · {state.Sample?.gazeInvalidReason}");
            EditorGUILayout.HelpBox("Last recorded values remain inspectable through gaps. Display freshness is not sensor-rate or fixation validation. Poses use the saved fixed recording origin.",MessageType.Info);
            EditorGUILayout.SelectableLabel(state.Sample == null ? "No sample recorded yet." : JsonUtility.ToJson(state.Sample,true),GUILayout.Height(180));
            EditorGUILayout.EndScrollView();
        }
    }
    void HandleViewport(Rect rect)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;
        if (e.type == EventType.ScrollWheel) { m_Renderer.Distance = Mathf.Clamp(m_Renderer.Distance + e.delta.y * 0.1f, 0.3f, 20); e.Use(); Repaint(); }
        if (e.type == EventType.MouseDrag && e.button == 1)
        { m_Renderer.Yaw += e.delta.x; m_Renderer.Pitch = Mathf.Clamp(m_Renderer.Pitch + e.delta.y, -85, 85); e.Use(); Repaint(); }
    }
}
