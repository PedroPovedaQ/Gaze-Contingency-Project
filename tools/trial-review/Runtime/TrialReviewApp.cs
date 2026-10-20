using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Read-only desktop inspection. This project contains no study scene or recorder.</summary>
public sealed partial class TrialReviewApp : MonoBehaviour
{
    TrialReplayTimeline m_Timeline;
    TrialReplayRenderer m_Renderer;
    TrialReplayAudioPreview m_Audio;
    TrialReviewSummary m_Summary;
    TrialReviewActors m_Actors;
    TrialReviewEnvironment m_Environment;
    TrialReviewGazeCorrection m_Correction;
    readonly TrialReviewVicinity m_Vicinity=new TrialReviewVicinity();
    bool m_ShowDetectionRing=true;
    bool m_ShowWalls=true,m_ShowDegrees=true,m_ShowRoom=true;
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
    GUIStyle m_Title, m_Label, m_MutedLabel, m_Button, m_Field, m_Heading, m_Code, m_WaitingTitle, m_DegreeLabel;
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
        if (Array.IndexOf(args, "--live") < 0) yield return Open(m_Path);
        else m_Path = "";
        int summaryIndex = Array.IndexOf(args, "--summary");
        if (m_Timeline != null && summaryIndex >= 0 && summaryIndex + 1 < args.Length)
        { m_ExportPath = args[summaryIndex + 1]; LoadSummary(); }
        int viewIndex=Array.IndexOf(args,"--view");
        if(m_Renderer!=null && viewIndex>=0 && viewIndex+1<args.Length)
        {m_Renderer.RecordedHead=args[viewIndex+1]=="first-person";m_Renderer.Overhead=args[viewIndex+1]=="overhead";}
        int timeIndex=Array.IndexOf(args,"--time");
        if(m_Timeline!=null && timeIndex>=0 && timeIndex+1<args.Length &&
            double.TryParse(args[timeIndex+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out double at) && !double.IsNaN(at)&&!double.IsInfinity(at))Seek(at);
        if (m_Renderer != null) m_CameraMode=m_Renderer.RecordedHead?0:m_Renderer.Overhead?2:1;
        if (Array.IndexOf(args,"--jev") >= 0 && m_Timeline != null)
        {
            m_Tab=3;
            var firstDecision=m_Tracks.Jev.Decisions.FirstOrDefault(d=>d.TrialId==m_Timeline.State.Trial?.trialId);
            if(firstDecision!=null)m_SelectedJev=firstDecision.Id;
        }
        if(Array.IndexOf(args,"--evaluation")>=0)m_Tab=4;
        InitializeLive(args);
    }
    public IEnumerator Open(string path)
    {
        if (m_Loading) yield break;
        LeaveLiveView();
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
            InitializeRunExport();
            ResetCamera();
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
        if (m_LiveOutput != null) PollLive();
        m_Tracks.Refresh(m_Timeline);
        UpdateRunExport();
        UpdateEvaluation();
        if (!m_Playing || m_Timeline == null || m_Loading) return;
        m_Time = Math.Min(m_Timeline.Duration, m_Time + Time.unscaledDeltaTime * m_Speed);
        m_Sequence = long.MaxValue;
        m_Renderer.Prepare(m_Timeline, m_Time);
        if (m_Time >= m_Timeline.Duration) m_Playing = false;
        Sync(false);
    }
    void Seek(double time, long sequence = long.MaxValue)
    {
        m_FollowLive = false;
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
        m_TimelineDrag = false;
        var first = m_Samples.FirstOrDefault(r => r.trialId == trial.trialId);
        Seek(first?.time ?? trial.time, first?.sequence ?? long.MaxValue);
        m_InspectScroll = m_EventScroll = Vector2.zero;
    }
    void LoadSummary()
    {
        try { if(m_ViewLive) throw new InvalidOperationException("Open the stopped saved recording before joining a final CSV export."); m_Summary = new TrialReviewSummary(m_ExportPath, m_Timeline, m_Hash); m_Error = null; }
        catch (Exception e) { m_Summary = null; m_Error = e.Message; }
    }
    void OnDestroy()
    {
        if(m_GazeDotTexture!=null)Destroy(m_GazeDotTexture);
        m_WaitingPyramid?.Dispose();
        StopRunExport();
        StopReceiver(); m_LiveStream?.Dispose(); m_Vicinity.Dispose(); m_Environment?.Dispose(); m_Actors?.Dispose(); m_Audio?.Dispose(); m_Renderer?.Dispose();
        foreach(var texture in new[]{m_Panel,m_ButtonTexture,m_SelectedTexture,m_HoverTexture})if(texture!=null)Destroy(texture);
    }
    void DrawDegrees()
    {
        if (!m_ShowDegrees || m_Environment == null) return;
        // Screen-facing labels stay readable from first-person, orbit and overhead cameras.
        // Angles belong to the saved layout, never to the current head/camera orientation.
        GUI.BeginGroup(m_Viewport);
        for (int plane = 0; plane < m_Environment.DegreePositions.Length; plane++)
        {
            var point = m_Renderer.Camera.WorldToViewportPoint(m_Environment.DegreePositions[plane]);
            if (point.z <= m_Renderer.Camera.nearClipPlane || point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1) continue;
            // Match the study's signed labels without changing saved wall IDs or geometry.
            float signedAzimuth=Mathf.DeltaAngle(0,plane*45);
            string label=plane == 0 ? "0° · FORWARD" : plane == 4 ? "±180°" : $"{signedAzimuth:+0;-0;0}°";
            float labelWidth=m_DegreeLabel.CalcSize(new GUIContent(label)).x+14;
            var rect = new Rect(point.x * m_Viewport.width - labelWidth/2, (1 - point.y) * m_Viewport.height - 13, labelWidth, 26);
            Fill(rect,new Color(.04f,.06f,.08f,.78f));
            GUI.Label(rect,label,m_DegreeLabel);
        }
        GUI.EndGroup();
    }
    void Browse(bool export)
    {
        m_ChooseStudyData=false; m_ChooseAttachment=false;
        m_TimelineDrag=false;GUIUtility.hotControl=0;
        m_FollowLive=false; m_ChooseExport=export; m_Browser=true; m_Playing=false;Sync(true);
        RefreshDirectory(Directory.Exists(m_BrowserPath)?m_BrowserPath:Path.GetDirectoryName(m_Path));
    }
    void RefreshDirectory(string path)
    {
        try
        {
            if(string.IsNullOrEmpty(path)) path=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path=Path.GetFullPath(path.Trim().Trim('"'));
            var directories=Directory.GetDirectories(path).Where(p=>!Path.GetFileName(p).StartsWith(".")).OrderBy(p=>p).ToArray();
            var files=Directory.GetFiles(path,m_ChooseAttachment?"*":"*.jsonl").OrderBy(p=>p).ToArray();
            m_BrowserPath=path;m_Directories=directories;m_Files=files;m_BrowserError=null;m_BrowseScroll=Vector2.zero;
        }
        catch(Exception e){m_BrowserError=e.Message;}
    }
    void Browser(Rect rect)
    {
        Panel(rect);GUILayout.BeginArea(new Rect(rect.x+18,rect.y+16,rect.width-36,rect.height-32));
        GUILayout.Label(m_ChooseAttachment?"Attach a file to this dataset":m_ChooseStudyData?"Choose study data folder (trial_summary.json)":m_ChooseExport?"Choose trial CSV export folder":"Open recorded trial package",m_Title);
        GUILayout.BeginHorizontal();m_BrowserPath=GUILayout.TextField(m_BrowserPath??"",m_Field);
        if(Button("Go")) RefreshDirectory(m_BrowserPath);
        if(Button("Up")) RefreshDirectory(Path.GetDirectoryName(m_BrowserPath));GUILayout.EndHorizontal();
        if(!string.IsNullOrEmpty(m_BrowserError))GUILayout.Label(m_BrowserError,m_Label);
        m_BrowseScroll=GUILayout.BeginScrollView(m_BrowseScroll);
        foreach(var path in m_Directories) if(Button("▸ "+Path.GetFileName(path))) {RefreshDirectory(path);break;}
        if(!m_ChooseExport&&!m_ChooseStudyData) foreach(var path in m_Files) if(Button(Path.GetFileName(path)))
        {
            m_Browser=false;
            if(m_ChooseAttachment){CatalogAction(()=>m_Catalog.Attach(m_Dataset,path));m_ChooseAttachment=false;m_DatasetPanel=true;}
            else StartCoroutine(Open(path));
            break;
        }
        GUILayout.EndScrollView();GUILayout.BeginHorizontal();
        if(m_ChooseExport && Button("Use this export folder")){m_ExportPath=m_BrowserPath;m_Browser=false;LoadSummary();}
        if(m_ChooseStudyData && Button("Use this study data folder")){AttachStudyFolder(m_BrowserPath);m_Browser=false;}
        if(Button("Cancel")){m_Browser=false;if(m_ChooseAttachment){m_ChooseAttachment=false;m_DatasetPanel=true;}}GUILayout.EndHorizontal();GUILayout.EndArea();
    }
}
