using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Read-only desktop inspection. This project contains no study scene or recorder.</summary>
public sealed class TrialReviewApp : MonoBehaviour
{
    TrialReplayTimeline m_Timeline;
    TrialReplayRenderer m_Renderer;
    TrialReplayAudioPreview m_Audio;
    TrialReviewSummary m_Summary;
    TrialReviewActors m_Actors;
    TrialReviewEnvironment m_Environment;
    TrialReviewGazeCorrection m_Correction;
    bool m_ShowWalls=true,m_ShowRoom=true;
    bool m_ShowBody = true, m_ShowEyeRay = true;
    readonly List<TrialReplayRecord> m_Samples = new List<TrialReplayRecord>();
    readonly List<TrialReplayRecord> m_Events = new List<TrialReplayRecord>();
    string m_Path = "", m_ExportPath = "", m_Hash, m_Error, m_BrowserPath, m_BrowserError;
    string[] m_Directories = Array.Empty<string>(), m_Files = Array.Empty<string>();
    bool m_Playing, m_Muted, m_Loading, m_Browser, m_ChooseExport;
    double m_Time;
    float m_Speed = 1;
    long m_Sequence = long.MaxValue;
    Vector2 m_TrialScroll, m_InspectScroll, m_EventScroll, m_BrowseScroll;
    int m_Tab;
    GUIStyle m_Title, m_Label, m_MutedLabel, m_Button, m_Field, m_Heading, m_Code, m_ViewToggle;
    int m_OptionColumns;
    Texture2D m_Panel;
    Rect m_Viewport;
    bool m_Ready;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.targetFrameRate = 60;
        gameObject.AddComponent<AudioListener>();
        m_Actors = new TrialReviewActors();
        m_Environment = new TrialReviewEnvironment();
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--recording");
        m_Path = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.Combine(Application.streamingAssetsPath, "Demo/recording.jsonl");
        yield return Open(m_Path);
        int summaryIndex = Array.IndexOf(args, "--summary");
        if (m_Timeline != null && summaryIndex >= 0 && summaryIndex + 1 < args.Length)
        { m_ExportPath = args[summaryIndex + 1]; LoadSummary(); }
        int viewIndex=Array.IndexOf(args,"--view");
        if(m_Renderer!=null && viewIndex>=0 && viewIndex+1<args.Length)
        {m_Renderer.RecordedHead=args[viewIndex+1]=="first-person";m_Renderer.Overhead=args[viewIndex+1]=="overhead";}
        int timeIndex=Array.IndexOf(args,"--time");
        if(m_Timeline!=null && timeIndex>=0 && timeIndex+1<args.Length &&
            double.TryParse(args[timeIndex+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double at) && !double.IsNaN(at)&&!double.IsInfinity(at))Seek(at);
    }
    public IEnumerator Open(string path)
    {
        if (m_Loading) yield break;
        m_Playing = false; m_Audio?.Stop(); m_Loading = true; m_Error = null;
        yield return null; // Paint the loading state before bounded synchronous Unity JSON parsing.
        TrialReplayRenderer renderer = null;
        TrialReplayAudioPreview audio = null;
        try
        {
            path = path.Trim().Trim('"');
            if (Directory.Exists(path)) path = Path.Combine(path, "recording.jsonl");
            string hash = TrialReviewSummary.Hash(path);
            var timeline = TrialReplayFile.Read(path);
            var correction = new TrialReviewGazeCorrection(timeline,hash);
            if (timeline.Trials.Count == 0) throw new InvalidDataException("Recording has no trial snapshots");
            if (hash != TrialReviewSummary.Hash(path)) throw new IOException("Recording changed while opening. Use a copied, stopped recording.");
            renderer = new TrialReplayRenderer(timeline) { RecordedHead = true, ShowGaze = false };
            audio = new TrialReplayAudioPreview(timeline, new TrialReviewAudioOutput());
            m_Audio?.Dispose(); m_Renderer?.Dispose();
            m_Timeline = timeline; m_Renderer = renderer; m_Audio = audio; m_Correction = correction;
            renderer = null; audio = null;
            m_Summary = null; m_ExportPath = ""; m_Hash = hash; m_Path = path;
            m_Samples.Clear(); m_Events.Clear();
            foreach (var record in timeline.Records)
                if (record.kind == "sample" || record.kind == "selection" && record.schema >= 2) m_Samples.Add(record);
            foreach (var record in timeline.Records)
                if (record.kind != "sample" && record.kind != "header" && record.kind != "trial") m_Events.Add(record);
            var first = m_Samples.FirstOrDefault(r => r.trialId == timeline.Trials[0].trialId);
            Seek(first?.time ?? timeline.Trials[0].time, first?.sequence ?? long.MaxValue);
            m_InspectScroll = m_TrialScroll = m_EventScroll = Vector2.zero;
        }
        catch (Exception e) { renderer?.Dispose(); audio?.Dispose(); m_Error = e.Message; Debug.LogWarning("[TrialReview] " + e.Message); }
        finally { m_Loading = false; }
    }
    void Update()
    {
        if (!m_Playing || m_Timeline == null || m_Loading) return;
        m_Time = Math.Min(m_Timeline.Duration, m_Time + Time.unscaledDeltaTime * m_Speed);
        m_Sequence = long.MaxValue;
        m_Renderer.Prepare(m_Timeline, m_Time);
        if (m_Time >= m_Timeline.Duration) m_Playing = false;
        Sync(false);
    }
    void Seek(double time, long sequence = long.MaxValue)
    {
        m_Time = Math.Max(0, Math.Min(m_Timeline.Duration, time)); m_Sequence = sequence;
        m_Renderer.Prepare(m_Timeline, m_Time, maxSequence: sequence); Sync(true);
    }
    void Sync(bool force) => m_Audio?.Sync(m_Time, m_Playing && !m_Muted, m_Speed, m_Sequence, force);
    void Step(int direction)
    {
        m_Playing = false;
        long cursor = m_Sequence == long.MaxValue ? m_Timeline.CurrentRecord?.sequence ?? -1 : m_Sequence;
        var sample = direction > 0 ? m_Samples.FirstOrDefault(r => r.sequence > cursor) :
            m_Samples.LastOrDefault(r => r.sequence < cursor || r.sequence == cursor && r.time < m_Time);
        if (sample != null) Seek(sample.time, sample.sequence); else Sync(true);
    }
    void SelectTrial(TrialReplayRecord trial)
    {
        m_Playing = false;
        var first = m_Samples.FirstOrDefault(r => r.trialId == trial.trialId);
        Seek(first?.time ?? trial.time, first?.sequence ?? long.MaxValue);
        m_InspectScroll = m_EventScroll = Vector2.zero;
    }
    void LoadSummary()
    {
        try { m_Summary = new TrialReviewSummary(m_ExportPath, m_Timeline, m_Hash); m_Error = null; }
        catch (Exception e) { m_Summary = null; m_Error = e.Message; }
    }
    void OnDestroy() { m_Environment?.Dispose(); m_Actors?.Dispose(); m_Audio?.Dispose(); m_Renderer?.Dispose(); if (m_Panel != null) Destroy(m_Panel); }
    void Styles()
    {
        if (m_Ready) return;
        m_Ready = true;
        m_Panel = new Texture2D(1, 1); m_Panel.SetPixel(0, 0, new Color(.075f, .092f, .12f)); m_Panel.Apply();
        m_Label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = false, normal = { textColor = new Color(.88f, .91f, .94f) } };
        m_Title = new GUIStyle(m_Label) { fontSize = 24, fontStyle = FontStyle.Bold };
        m_Heading = new GUIStyle(m_Label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.45f, .83f, .78f) } };
        m_MutedLabel = new GUIStyle(m_Label) { fontSize = 12, normal = { textColor = new Color(.64f, .7f, .77f) } };
        m_Button = new GUIStyle(GUI.skin.button) { fontSize = 13, padding = new RectOffset(10, 10, 7, 7), richText = false };
        m_ViewToggle = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(42, 10, 8, 8), richText = false };
        m_Field = new GUIStyle(GUI.skin.textField) { fontSize = 13, padding = new RectOffset(8, 8, 7, 7) };
        m_Code = new GUIStyle(m_MutedLabel) { fontSize = 12, wordWrap = true };
    }
    bool Button(string label, params GUILayoutOption[] options) => GUILayout.Button(label, m_Button, options);
    void Panel(Rect rect) => GUI.DrawTexture(rect, m_Panel);
    void OnGUI()
    {
        Styles();
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(.04f,.05f,.07f), 0, 0);
        // Logical minimum size keeps every control accessible on small/retina windows.
        float scale = Mathf.Min(1, Mathf.Min(Screen.width / 1150f, Screen.height / 700f));
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.enabled = !m_Browser && !m_Loading;
        GUILayout.BeginArea(new Rect(22, 16, width - 44, 116));
        GUILayout.BeginHorizontal(); GUILayout.Label("Trial Review", m_Title); GUILayout.FlexibleSpace();
        GUILayout.Label("LOCAL  /  READ ONLY", m_Heading); GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal(); m_Path = GUILayout.TextField(m_Path, m_Field);
        if (Button("Browse…", GUILayout.Width(95))) Browse(false);
        if (Button("Open run", GUILayout.Width(95))) StartCoroutine(Open(m_Path));
        if (Button("Demo", GUILayout.Width(75))) StartCoroutine(Open(Path.Combine(Application.streamingAssetsPath, "Demo/recording.jsonl")));
        GUILayout.EndHorizontal();
        GUILayout.Label(m_Loading ? "Loading recording…" : m_Timeline == null ? "Open a saved replay package to begin." :
            $"{m_Timeline.Header.sourceType ?? "Unknown source"}  ·  {m_Timeline.Header.recordingId}  ·  {m_Timeline.Trials.Count} trials  ·  {m_Timeline.Duration:F1}s", m_MutedLabel);
        if (!string.IsNullOrEmpty(m_Timeline?.Warning)) GUILayout.Label("INCOMPLETE RECORDING · " + m_Timeline.Warning, m_Heading);
        GUILayout.EndArea();
        float top = 140, bottom = height - 120, rail = 210, inspector = 320;
        Panel(new Rect(16, top, rail, bottom - top));
        Panel(new Rect(width - inspector - 16, top, inspector, bottom - top));
        float viewWidth=width-rail-inspector-62;
        m_OptionColumns=Mathf.Clamp(Mathf.FloorToInt(viewWidth/190),2,4);
        float optionsHeight=Mathf.Ceil(7f/m_OptionColumns)*48+26;
        m_Viewport = new Rect(rail + 30, top + 46, viewWidth, bottom - top - 46 - optionsHeight);
        if (m_Timeline != null)
        {
            Trials(new Rect(24, top + 10, rail - 16, bottom - top - 20));
            View(new Rect(m_Viewport.x, top, m_Viewport.width, 40));
            Inspector(new Rect(width - inspector - 6, top + 10, inspector - 20, bottom - top - 20));
            Transport(new Rect(24, bottom + 8, width - 48, 106));
        }
        if (!string.IsNullOrEmpty(m_Error)) GUI.Label(new Rect(m_Viewport.x, bottom - 32, Math.Max(400,m_Viewport.width), 30), m_Error, m_MutedLabel);
        GUI.enabled = true;
        if (m_Loading) GUI.Box(new Rect(width/2-170, height/2-35,340,70), "Loading recording…");
        if (m_Browser) Browser(new Rect(width/2-360, 100, 720, height-180));
    }
    void Trials(Rect rect)
    {
        GUILayout.BeginArea(rect); GUILayout.Label("TRIALS", m_Heading);
        m_TrialScroll = GUILayout.BeginScrollView(m_TrialScroll);
        for (int i=0; i<m_Timeline.Trials.Count; i++)
        {
            var trial = m_Timeline.Trials[i];
            GUI.backgroundColor = trial == m_Timeline.State.Trial ? new Color(.25f,.65f,.6f) : Color.white;
            if (Button($"{(trial.practice ? "Practice" : "Trial")} {(trial.practice ? m_Timeline.Trials.Take(i+1).Count(t=>t.practice) : trial.round+1)}\n{trial.voice ?? "Voice unknown"} · |θ| {trial.absoluteTheta}°")) SelectTrial(trial);
            GUI.backgroundColor = Color.white;
        }
        GUILayout.EndScrollView();
        GUILayout.Label("3D reconstruction\nFixed preview FOV; no room video. Recorded values are not interpolated.", m_MutedLabel);
        GUILayout.EndArea();
    }
    void View(Rect controls)
    {
        GUILayout.BeginArea(controls); GUILayout.BeginHorizontal();
        if (Button("First person")) { m_Renderer.RecordedHead = true; m_Renderer.Overhead = false; }
        if (Button("Third person")) { m_Renderer.RecordedHead = false; m_Renderer.Overhead = false; }
        if (Button("Overhead")) { m_Renderer.RecordedHead = false; m_Renderer.Overhead = true; }
        GUILayout.EndHorizontal(); GUILayout.EndArea();
        m_Renderer.Prepare(m_Timeline, m_Time, maxSequence: m_Sequence);
        m_Environment?.Prepare(m_Timeline.State.Trial,m_ShowWalls,m_ShowRoom);
        m_Actors?.Prepare(m_Timeline,m_Time,m_Renderer.RecordedHead,m_ShowBody,m_Renderer.ShowControllers,m_ShowEyeRay,m_Correction);
        if(!m_Renderer.RecordedHead && m_Renderer.SampleCurrent && TrialReplayRenderer.HeadUsable(m_Timeline.State.Sample))
            m_Renderer.Camera.transform.position+=m_Timeline.State.Sample.headPosition+Vector3.down*.5f;
        if (Event.current.type == EventType.Repaint) GUI.DrawTexture(m_Viewport, m_Renderer.Render(m_Viewport), ScaleMode.StretchToFill, false);
        string caption = m_Renderer.RecordedHead ? m_Renderer.UsingRecordedHead ? "RECORDED HEAD" : "HEAD POSE UNAVAILABLE · overview shown" : m_Renderer.Overhead ? "OVERHEAD" : "THIRD PERSON · right-drag / scroll";
        GUI.Label(new Rect(m_Viewport.x+12,m_Viewport.y+12,m_Viewport.width-24,40),caption,m_Heading);
        if(!m_Renderer.RecordedHead)GUI.Label(new Rect(m_Viewport.x+12,m_Viewport.y+42,m_Viewport.width-24,40),
            "Arms / torso / chair estimated · head / controllers recorded",m_MutedLabel);
        if(m_ShowEyeRay && m_Actors!=null && !float.IsNaN(m_Actors.GazeOriginOffset))
            GUI.Label(new Rect(m_Viewport.x+12,m_Viewport.y+72,m_Viewport.width-24,40),
                (m_Correction.Supported ? m_Actors.CorrectionApplied ?
                    $"X-flip undone · eye–head {m_Actors.CorrectedHeadOffset*100:F1} cm · calibration unverified" :
                    "Corrected gaze unavailable: missing valid XR reference" :
                    $"Eye ray: display origin only · raw origin offset {m_Actors.GazeOriginOffset:F2} m")+
                (m_Actors.ArmReachLimited?" · arm reach limited":""),m_MutedLabel);
        m_ShowEyeRay = ViewToggle(0,m_ShowEyeRay,m_Correction.Supported?"Corrected gaze":"Eye ray (display)");
        m_Renderer.ShowGaze = ViewToggle(1,m_Renderer.ShowGaze,"Raw gaze");
        m_Renderer.ShowControllers = ViewToggle(2,m_Renderer.ShowControllers,"Vive controllers");
        m_ShowBody = ViewToggle(3,m_ShowBody,"Human model");
        m_Renderer.ShowTarget = ViewToggle(4,m_Renderer.ShowTarget,"Reveal target");
        m_ShowWalls = ViewToggle(5,m_ShowWalls,"Study walls");
        m_ShowRoom = ViewToggle(6,m_ShowRoom,"Room background");
        GUI.Label(new Rect(m_Viewport.x,m_Viewport.yMax+8+Mathf.Ceil(7f/m_OptionColumns)*48,m_Viewport.width,20),
            "Inferred walls · illustrative room",m_MutedLabel);
        var e=Event.current;
        if (m_Viewport.Contains(e.mousePosition) && !m_Renderer.RecordedHead)
        {
            if (e.type == EventType.MouseDrag && e.button==1) { m_Renderer.Yaw += e.delta.x; m_Renderer.Pitch=Mathf.Clamp(m_Renderer.Pitch+e.delta.y,-85,85); e.Use(); }
            if (e.type == EventType.ScrollWheel) { m_Renderer.Distance=Mathf.Clamp(m_Renderer.Distance+e.delta.y*.2f,.3f,30); e.Use(); }
        }
    }
    bool ViewToggle(int index,bool value,string label)
    {
        float width=(m_Viewport.width-(m_OptionColumns-1)*8)/m_OptionColumns;
        var rect=new Rect(m_Viewport.x+(index%m_OptionColumns)*(width+8),m_Viewport.yMax+8+(index/m_OptionColumns)*48,width,42);
        // The entire labeled card is clickable; a large checkbox communicates state without relying on color alone.
        bool selected=GUI.Toggle(rect,value,label,m_ViewToggle);
        var box=new Rect(rect.x+11,rect.y+10,22,22);
        GUI.DrawTexture(box,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.57f,.68f,.73f),0,0);
        GUI.DrawTexture(new Rect(box.x+2,box.y+2,18,18),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.06f,.09f,.12f),0,0);
        if(selected)GUI.DrawTexture(new Rect(box.x+5,box.y+5,12,12),Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.45f,.83f,.78f),0,0);
        return selected;
    }
    void Inspector(Rect rect)
    {
        GUILayout.BeginArea(rect);
        int tab = GUILayout.Toolbar(m_Tab,new[]{"At cursor","Events","Final data"});
        if (tab!=m_Tab) { m_Tab=tab; m_InspectScroll=Vector2.zero; }
        if (m_Tab == 1)
        {
            m_EventScroll=GUILayout.BeginScrollView(m_EventScroll);
            foreach (var record in m_Events)
            {
                if (record.trialId != m_Timeline.State.Trial?.trialId && record.kind != "end") continue;
                string detail=record.kind=="selection" ? (record.correct?"Correct":"Incorrect")+" · "+record.objectId : record.text;
                if (Button($"{record.time:F3}s · {record.kind}\n{detail}")) { m_Playing=false; Seek(record.time,record.sequence); }
            }
            GUILayout.EndScrollView();
        }
        else if (m_Tab == 2)
        {
            GUILayout.Label("FINAL TRIAL / BLOCK CONTEXT",m_Heading);
            GUILayout.Label("These results do not change with the cursor. Block surveys repeat across trials.",m_MutedLabel);
            GUILayout.BeginHorizontal();
            if (Button("Choose CSV export…")) Browse(true);
            GUILayout.EndHorizontal();
            var row=m_Summary?.For(m_Timeline.State.Trial);
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            if (row==null) GUILayout.Label("No matched summary loaded. Select the export folder containing manifest.json and trials.csv.",m_Label);
            else foreach (var pair in row) { GUILayout.Label(pair.Key,m_Heading); GUILayout.Label(string.IsNullOrEmpty(pair.Value)?"— unavailable":pair.Value,m_Label); GUILayout.Space(7); }
            GUILayout.EndScrollView();
        }
        else
        {
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            var state=m_Timeline.State; var sample=state.Sample;
            GUILayout.Label(m_Renderer.SampleCurrent?"CURRENT OBSERVATION":"NO CURRENT POSE",m_Heading);
            GUILayout.Label($"Source line {m_Timeline.SampleSourceLine}\nEvent: {m_Timeline.CurrentRecord?.kind}\nSearch active: {state.Searching}\nObjects visible: {state.Visible}",m_Label);
            if (sample!=null)
            {
                GUILayout.Label($"Sample age: {m_Time-sample.time:F3}s\nActive search: {sample.searchSeconds:F3}s\nHead tracked: {sample.headTracked}\nGaze tracked: {sample.gazeTracked}\nRight trigger: {sample.rightController?.triggerHeld ?? -1}\nRight hit: {sample.rightController?.hoveredId ?? "—"}\nRecorded gaze hit: {sample.hoveredId ?? "—"}",m_Label);
                GUILayout.Space(10); GUILayout.Label("DECODED SAMPLE",m_Heading);
                GUILayout.Label("Historical values remain visible through gaps. -1 means unknown. Absent fields may show schema defaults below. Ray hits are not validated fixations.",m_MutedLabel);
                GUILayout.TextArea(JsonUtility.ToJson(sample,true),m_Code);
            }
            if (!string.IsNullOrEmpty(m_Timeline.Warning)) GUILayout.Label(m_Timeline.Warning,m_MutedLabel);
            if (!string.IsNullOrEmpty(m_Audio.Warning)) GUILayout.Label(m_Audio.Warning,m_MutedLabel);
            GUILayout.EndScrollView();
        }
        GUILayout.EndArea();
    }
    void Transport(Rect rect)
    {
        GUILayout.BeginArea(rect); GUILayout.BeginHorizontal();
        if (Button(m_Playing?"Pause":"Play",GUILayout.Width(75))) { m_Playing=!m_Playing; Sync(true); }
        if (Button("‹ Sample")) Step(-1);
        if (Button("Sample ›")) Step(1);
        if (Button("Next event")) { var next=m_Events.FirstOrDefault(r=>r.sequence>(m_Timeline.CurrentRecord?.sequence??-1)); if(next!=null){m_Playing=false;Seek(next.time,next.sequence);} }
        GUILayout.FlexibleSpace();
        foreach(float speed in new[]{.25f,.5f,1f,2f}) if(Button(speed+"×",GUILayout.Width(48))){m_Speed=speed;Sync(true);}
        bool mute=GUILayout.Toggle(m_Muted,"Mute",GUILayout.Width(65)); if(mute!=m_Muted){m_Muted=mute;Sync(true);}
        GUILayout.Label($"{m_Time:F3} / {m_Timeline.Duration:F3}s",m_Label,GUILayout.Width(190));
        GUILayout.EndHorizontal();
        float nextTime=GUILayout.HorizontalSlider((float)m_Time,0,(float)Math.Max(.001,m_Timeline.Duration),GUILayout.Height(25));
        if(Math.Abs(nextTime-(float)m_Time)>.00001f){m_Playing=false;Seek(nextTime);}
        GUILayout.Label($"{m_Speed}× · {(m_Speed==1?"Saved audio follows playback":"Audio muted away from 1×")} · 100 ms pose freshness display rule · No study responses are recorded here.",m_MutedLabel);
        GUILayout.EndArea();
    }
    void Browse(bool export)
    {
        m_ChooseExport=export; m_Browser=true; m_Playing=false;Sync(true);
        RefreshDirectory(Directory.Exists(m_BrowserPath)?m_BrowserPath:Path.GetDirectoryName(m_Path));
    }
    void RefreshDirectory(string path)
    {
        try
        {
            if(string.IsNullOrEmpty(path)) path=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path=Path.GetFullPath(path.Trim().Trim('"'));
            var directories=Directory.GetDirectories(path).Where(p=>!Path.GetFileName(p).StartsWith(".")).OrderBy(p=>p).ToArray();
            var files=Directory.GetFiles(path,"*.jsonl").OrderBy(p=>p).ToArray();
            m_BrowserPath=path;m_Directories=directories;m_Files=files;m_BrowserError=null;m_BrowseScroll=Vector2.zero;
        }
        catch(Exception e){m_BrowserError=e.Message;}
    }
    void Browser(Rect rect)
    {
        Panel(rect);GUILayout.BeginArea(new Rect(rect.x+18,rect.y+16,rect.width-36,rect.height-32));
        GUILayout.Label(m_ChooseExport?"Choose trial CSV export folder":"Open recorded trial package",m_Title);
        GUILayout.BeginHorizontal();m_BrowserPath=GUILayout.TextField(m_BrowserPath??"",m_Field);
        if(Button("Go")) RefreshDirectory(m_BrowserPath);
        if(Button("Up")) RefreshDirectory(Path.GetDirectoryName(m_BrowserPath));GUILayout.EndHorizontal();
        if(!string.IsNullOrEmpty(m_BrowserError))GUILayout.Label(m_BrowserError,m_Label);
        m_BrowseScroll=GUILayout.BeginScrollView(m_BrowseScroll);
        foreach(var path in m_Directories) if(Button("▸ "+Path.GetFileName(path))) {RefreshDirectory(path);break;}
        if(!m_ChooseExport) foreach(var path in m_Files) if(Button(Path.GetFileName(path))){m_Browser=false;StartCoroutine(Open(path));break;}
        GUILayout.EndScrollView();GUILayout.BeginHorizontal();
        if(m_ChooseExport && Button("Use this export folder")){m_ExportPath=m_BrowserPath;m_Browser=false;LoadSummary();}
        if(Button("Cancel"))m_Browser=false;GUILayout.EndHorizontal();GUILayout.EndArea();
    }
}
