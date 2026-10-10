using System;
using System.IO;
using System.Linq;
using UnityEngine;

// Desktop presentation only: recorded poses, audio and timing stay owned by the replay classes.
public sealed partial class TrialReviewApp
{
    readonly TrialReviewTracks m_Tracks = new TrialReviewTracks();
    readonly Color m_Background=new Color(.045f,.06f,.075f), m_Surface=new Color(.075f,.095f,.115f),
        m_Border=new Color(.19f,.24f,.28f), m_Accent=new Color(.38f,.84f,.77f), m_Orange=new Color(1,.59f,.25f);
    GUIStyle m_Center, m_Small, m_Chip, m_ActiveChip, m_Row, m_ActiveRow;
    string m_TrialQuery="", m_ExportNotice="";
    int m_TrialFilter, m_CameraMode;
    bool m_RawExpanded, m_RunTimeline, m_TimelineDrag;
    float m_Volume=.8f;
    double m_DragStart, m_DragEnd;
    TrialReplayRecord m_DragTrial;
    Texture2D m_ButtonTexture, m_SelectedTexture, m_HoverTexture;

    Texture2D Solid(Color color)
    { var texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();return texture; }
    void Styles()
    {
        if(m_Ready)return;m_Ready=true;
        m_Panel=Solid(m_Surface);m_ButtonTexture=Solid(new Color(.10f,.13f,.16f));
        m_SelectedTexture=Solid(m_Accent);m_HoverTexture=Solid(new Color(.17f,.23f,.26f));
        m_Label=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true,richText=false,normal={textColor=new Color(.9f,.94f,.96f)}};
        m_MutedLabel=new GUIStyle(m_Label){fontSize=12,normal={textColor=new Color(.61f,.68f,.73f)}};
        m_Title=new GUIStyle(m_Label){fontSize=23,fontStyle=FontStyle.Bold};
        m_Heading=new GUIStyle(m_Label){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=m_Accent}};
        m_Center=new GUIStyle(m_Label){alignment=TextAnchor.MiddleCenter,wordWrap=false};
        m_Small=new GUIStyle(m_MutedLabel){alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip};
        m_Button=new GUIStyle(GUI.skin.button){fontSize=14,richText=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,9,9),border=new RectOffset(0,0,0,0)};
        m_Button.normal.background=m_ButtonTexture;m_Button.normal.textColor=m_Label.normal.textColor;
        m_Button.hover.background=m_HoverTexture;m_Button.hover.textColor=Color.white;
        m_Button.active.background=m_SelectedTexture;m_Button.active.textColor=m_Background;
        m_Button.focused.background=m_HoverTexture;
        m_Chip=new GUIStyle(m_Button){fontSize=13,padding=new RectOffset(8,8,7,7)};
        m_ActiveChip=new GUIStyle(m_Chip);m_ActiveChip.normal.background=m_SelectedTexture;m_ActiveChip.normal.textColor=m_Background;
        m_Row=new GUIStyle(m_Button){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=14,padding=new RectOffset(12,10,9,9)};
        m_ActiveRow=new GUIStyle(m_Row);m_ActiveRow.normal.background=m_HoverTexture;m_ActiveRow.normal.textColor=m_Accent;
        m_Field=new GUIStyle(GUI.skin.textField){fontSize=14,padding=new RectOffset(9,9,9,9),normal={textColor=Color.white,background=m_ButtonTexture}};
        m_Code=new GUIStyle(m_MutedLabel){fontSize=11,wordWrap=true};
        m_WaitingTitle=new GUIStyle(m_Heading){alignment=TextAnchor.MiddleCenter};
        m_DegreeLabel=new GUIStyle(m_Heading){fontSize=13,alignment=TextAnchor.MiddleCenter,wordWrap=false};
        AudioListener.volume=m_Volume;
    }
    bool Button(string label,params GUILayoutOption[] options)=>GUILayout.Button(label,m_Button,options);
    bool Action(Rect rect,string label,bool active=false)=>GUI.Button(rect,label,active?m_ActiveChip:m_Chip);
    void Fill(Rect rect,Color color)=>GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,color,0,0);
    void Panel(Rect rect)=>Fill(rect,m_Surface);
    static string Clock(double seconds)
    { seconds=Math.Max(0,seconds);return $"{(int)(seconds/60):00}:{seconds%60:00.00}"; }
    static string Voice(string voice)=>string.IsNullOrEmpty(voice)?"Unknown voice":voice.Replace('_',' ');
    static string EventName(string kind)=>string.IsNullOrEmpty(kind)?"Event":kind.Replace('_',' ');

    void OnGUI()
    {
        Styles();
        // Keep controls comfortably sized on both retina and small desktop windows.
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float width=Screen.width/scale,height=Screen.height/scale;
        Fill(new Rect(0,0,width,height),m_Background);
        GUI.enabled=!m_Browser&&!m_Loading;
        Header(width);
        if(m_LivePanel)LiveControls(width);
        float top=m_LivePanel?164:78,bottom=height-230,rail=204,inspector=274;
        Panel(new Rect(0,top,rail,bottom-top));Panel(new Rect(width-inspector,top,inspector,bottom-top));
        float left=rail+14,viewWidth=width-inspector-14-left;
        m_OptionColumns=Mathf.Clamp(Mathf.FloorToInt(viewWidth/145),3,8);
        float optionsHeight=Mathf.Ceil(8f/m_OptionColumns)*42+26;
        m_Viewport=new Rect(left,top+48,viewWidth,bottom-top-48-optionsHeight);
        CameraControls(new Rect(left,top,viewWidth,40));
        if(m_Timeline!=null)
        {
            Trials(new Rect(12,top+12,rail-24,bottom-top-24));
            View();Inspector(new Rect(width-inspector+14,top+8,inspector-28,bottom-top-18));
        }
        else
        {
            GUI.Label(new Rect(18,top+16,170,25),"TRIALS",m_Heading);
            GUI.Label(new Rect(18,top+52,170,90),"Open a saved run or follow the headset to load trials.",m_MutedLabel);
        }
        if(WaitingForHeadset||m_Timeline==null)
        {
            Fill(m_Viewport,m_Background);
            m_WaitingTitle.fontSize=Mathf.RoundToInt(Mathf.Clamp(m_Viewport.width*.047f,30,56));
            var title=new GUIContent(WaitingForHeadset?"Waiting for headset signal":"Open a run to begin");
            float titleHeight=m_WaitingTitle.CalcHeight(title,m_Viewport.width-48);
            GUI.Label(new Rect(m_Viewport.x+24,m_Viewport.center.y-titleHeight/2,m_Viewport.width-48,titleHeight),title,m_WaitingTitle);
        }
        Transport(new Rect(14,bottom+12,width-28,208));
        if(!string.IsNullOrEmpty(m_Error))
        {
            Panel(new Rect(left,top+50,viewWidth,64));
            GUI.Label(new Rect(left+12,top+56,viewWidth-24,52),m_Error,m_Label);
        }
        GUI.enabled=true;
        if(m_Loading){Panel(new Rect(width/2-190,height/2-40,380,80));GUI.Label(new Rect(width/2-180,height/2-30,360,60),"Loading recording…",m_Center);}
        if(m_Browser)Browser(new Rect(width/2-380,80,760,height-160));
    }
    void Header(float width)
    {
        Panel(new Rect(0,0,width,64));Fill(new Rect(0,63,width,1),m_Border);
        GUI.Label(new Rect(20,17,180,32),"Trial Review",m_Title);
        string participant=m_Timeline?.Header.participantCode;
        if(string.IsNullOrEmpty(participant))participant=m_Timeline?.State.Trial?.participantCode;
        int run=m_Timeline?.State.Trial?.runNumber??m_Timeline?.Header.runNumber??0;
        string name=m_Timeline==null?"Choose a session":$"{(string.IsNullOrEmpty(participant)?"Recorded session":participant)} / {(run>0?$"Run {run}":"Session")}";
        if(Action(new Rect(222,14,260,36),name))Browse(false);
        string status=m_FollowLive?(WaitingForHeadset?"Waiting for signal":"Following live"):m_ViewLive?"Reviewing archive":"Saved replay";
        GUI.Label(new Rect(496,20,190,24),status,m_Heading);
        if(Action(new Rect(width-540,14,124,36),"Open run…"))Browse(false);
        if(Action(new Rect(width-408,14,76,36),"Demo"))StartCoroutine(Open(Path.Combine(Application.streamingAssetsPath,"Demo/recording.jsonl")));
        if(Action(new Rect(width-324,14,144,36),"Wi-Fi settings",m_LivePanel))m_LivePanel=!m_LivePanel;
        if(Action(new Rect(width-172,14,152,36),"Follow live",m_FollowLive))
        {m_LivePanel=true;ConnectLive();}
    }
    void CameraControls(Rect rect)
    {
        GUI.Label(new Rect(rect.x,rect.y+10,145,25),"3D reconstruction",m_Label);
        float buttonWidth=(rect.width-247)/3;
        string[] names={"First person","Third person","Overhead"};
        for(int i=0;i<3;i++)if(Action(new Rect(rect.x+150+i*(buttonWidth+4),rect.y,buttonWidth,38),names[i],m_CameraMode==i))m_CameraMode=i;
        if(Action(new Rect(rect.xMax-81,rect.y,81,38),"Reset view"))
        {ResetCamera();}
    }
    void ResetCamera()
    {
        if(m_Renderer==null)return;
        var trial=m_Timeline?.State.Trial??m_Timeline?.Trials.FirstOrDefault();
        m_Environment?.Prepare(trial,m_ShowWalls,m_ShowRoom);
        float radius=m_Environment?.Radius??3;
        m_Renderer.Yaw=35;m_Renderer.Pitch=28;
        m_Renderer.Distance=m_CameraMode==2?Mathf.Max(3,radius*3.2f):Mathf.Max(2.5f,radius*2.4f);
    }
    int TrialNumber(TrialReplayRecord trial)=>trial.practice
        ?m_Timeline.Trials.TakeWhile(t=>t!=trial).Count(t=>t.practice)+1:trial.round+1;
    void Trials(Rect rect)
    {
        GUILayout.BeginArea(rect);GUILayout.Label("TRIALS",m_Heading);GUILayout.Space(8);
        m_TrialQuery=GUILayout.TextField(m_TrialQuery,m_Field,GUILayout.Height(36));
        if(string.IsNullOrEmpty(m_TrialQuery))GUI.Label(new Rect(10,35,150,28),"Search trials…",m_Small);
        GUILayout.BeginHorizontal();
        string[] filters={"All","Study","Practice"};
        for(int i=0;i<3;i++)if(GUILayout.Button(filters[i],m_TrialFilter==i?m_ActiveChip:m_Chip))m_TrialFilter=i;
        GUILayout.EndHorizontal();GUILayout.Space(10);
        m_TrialScroll=GUILayout.BeginScrollView(m_TrialScroll);
        string group=null;int count=0;
        foreach(var trial in m_Timeline.Trials)
        {
            if(!TrialReviewTracks.Matches(trial,m_TrialQuery,m_TrialFilter))continue;
            string next=trial.practice?"PRACTICE":$"BLOCK {trial.block} · {Voice(trial.voice)}";
            if(next!=group){GUILayout.Space(10);GUILayout.Label(next,m_Heading);group=next;}
            var track=m_Tracks.For(trial);
            string duration=track==null?"":Clock(track.End-trial.time);
            bool selected=trial==m_Timeline.State.Trial;
            if(GUILayout.Button($"{(selected?"› ":"")}{TrialNumber(trial):00}     {duration}\n|θ| {trial.absoluteTheta}°  ·  {(track?.Completed==true?"Selected":"Recorded")}",selected?m_ActiveRow:m_Row,GUILayout.Height(59)))SelectTrial(trial);
            count++;
        }
        if(count==0)GUILayout.Label("No matching trials",m_MutedLabel);
        GUILayout.EndScrollView();GUILayout.Space(6);
        GUILayout.Label($"{count} trials · {m_Timeline.Header.sourceType}\nDurations show recorded elapsed time.",m_MutedLabel);
        GUILayout.EndArea();
    }
    void View()
    {
        bool recordedGaze=m_Timeline.Header.gazeRayVersion==TrialReplayGaze.Version;
        m_Renderer.RecordedHead=m_CameraMode==0;m_Renderer.Overhead=m_CameraMode==2;
        m_Renderer.ShowUnalignedGaze=recordedGaze;
        m_Renderer.Prepare(m_Timeline,m_Time,maxSequence:m_Sequence);
        m_Environment.Prepare(m_Timeline.State.Trial,m_ShowWalls,m_ShowRoom);
        m_Actors.Prepare(m_Timeline,m_Time,m_Renderer.RecordedHead,m_ShowBody,m_Renderer.ShowControllers,m_ShowEyeRay,m_Correction);
        if(!m_Renderer.RecordedHead&&m_Renderer.SampleCurrent&&TrialReplayRenderer.HeadUsable(m_Timeline.State.Sample))
            m_Renderer.Camera.transform.position+=m_Timeline.State.Sample.headPosition+Vector3.down*.5f;
        if(Event.current.type==EventType.Repaint)GUI.DrawTexture(m_Viewport,m_Renderer.Render(m_Viewport),ScaleMode.StretchToFill,false);
        DrawDegrees();
        if(m_Renderer.RecordedHead&&!m_Renderer.UsingRecordedHead)GUI.Label(new Rect(m_Viewport.x+12,m_Viewport.y+12,m_Viewport.width-24,30),"Head pose unavailable · overview shown",m_Heading);
        m_ShowEyeRay=ViewToggle(0,m_ShowEyeRay,recordedGaze?"Eye gaze":m_Correction.Supported?"Corrected gaze":"Eye ray (display)");
        m_Renderer.ShowControllers=ViewToggle(1,m_Renderer.ShowControllers,"Controllers");
        m_ShowBody=ViewToggle(2,m_ShowBody,"Human model");
        m_ShowWalls=ViewToggle(3,m_ShowWalls,"Walls");
        m_ShowDegrees=ViewToggle(4,m_ShowDegrees,"Degrees");
        m_ShowRoom=ViewToggle(5,m_ShowRoom,"Room");
        m_Renderer.ShowTarget=ViewToggle(6,m_Renderer.ShowTarget,"Reveal target");
        m_Renderer.ShowGaze=ViewToggle(7,m_Renderer.ShowGaze,recordedGaze?"Before alignment":"Raw gaze");
        GUI.Label(new Rect(m_Viewport.x,m_Viewport.yMax+8+Mathf.Ceil(8f/m_OptionColumns)*42,m_Viewport.width,20),
            "Reconstructed scene · body / chair estimated · walls inferred · right-drag to orbit, scroll to zoom",m_MutedLabel);
        var e=Event.current;
        if(GUI.enabled&&m_Viewport.Contains(e.mousePosition)&&!m_Renderer.RecordedHead&&!WaitingForHeadset)
        {
            if(e.type==EventType.MouseDrag&&e.button==1){m_Renderer.Yaw+=e.delta.x;m_Renderer.Pitch=Mathf.Clamp(m_Renderer.Pitch+e.delta.y,-85,85);e.Use();}
            if(e.type==EventType.ScrollWheel){m_Renderer.Distance=Mathf.Clamp(m_Renderer.Distance+e.delta.y*.2f,.3f,30);e.Use();}
        }
    }
    bool ViewToggle(int index,bool value,string label)
    {
        float width=(m_Viewport.width-(m_OptionColumns-1)*6)/m_OptionColumns;
        var rect=new Rect(m_Viewport.x+(index%m_OptionColumns)*(width+6),m_Viewport.yMax+7+(index/m_OptionColumns)*42,width,35);
        if(Action(rect,(value?"●  ":"○  ")+label,value))value=!value;
        return value;
    }
    void Metric(string label,string value)
    {
        GUILayout.BeginHorizontal();GUILayout.Label(label,m_MutedLabel,GUILayout.Width(96));
        GUILayout.Label(string.IsNullOrEmpty(value)?"Unavailable":value,m_Label);GUILayout.EndHorizontal();GUILayout.Space(5);
    }
    void Inspector(Rect rect)
    {
        GUILayout.BeginArea(rect);GUILayout.BeginHorizontal();
        string[] tabs={"At cursor","Events","Final data"};
        for(int i=0;i<tabs.Length;i++)if(GUILayout.Button(tabs[i],m_Tab==i?m_ActiveChip:m_Chip)){m_Tab=i;m_InspectScroll=Vector2.zero;}
        GUILayout.EndHorizontal();GUILayout.Space(18);
        var state=m_Timeline.State;var trial=state.Trial;var sample=state.Sample;
        if(m_Tab==1)
        {
            m_EventScroll=GUILayout.BeginScrollView(m_EventScroll);
            var track=m_Tracks.For(trial);
            if(track!=null)foreach(var r in track.Events)
            {
                if(GUILayout.Button($"{Clock(r.time-trial.time)} · {EventName(r.kind)}\n{r.text ?? r.detail}",m_Row))
                {m_Playing=false;Seek(r.time,r.sequence);}
            }
            GUILayout.EndScrollView();
        }
        else if(m_Tab==2)
        {
            GUILayout.Label("FINAL TRIAL RESULTS",m_Heading);
            GUILayout.Label("Final summary context, not values at the cursor. Block surveys repeat across trials.",m_MutedLabel);
            if(Button("Choose CSV export…"))Browse(true);
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            var row=m_Summary?.For(trial);
            if(row==null)GUILayout.Label("No matching summary loaded. Choose a completed export with manifest.json and trials.csv.",m_Label);
            else foreach(var pair in row){GUILayout.Label(pair.Key,m_Heading);GUILayout.Label(string.IsNullOrEmpty(pair.Value)?"Unavailable":pair.Value,m_Label);GUILayout.Space(8);}
            GUILayout.EndScrollView();
        }
        else
        {
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            GUILayout.Label(trial==null?"No trial":$"{(trial.practice?"Practice":"Trial")} {TrialNumber(trial):00}",m_Title);GUILayout.Space(14);
            Metric("Voice",Voice(trial?.voice));Metric("Elapsed",Clock(m_Time-(trial?.time??0)));Metric("Search",state.Searching?"Active":"Inactive");
            GUILayout.Space(12);GUILayout.Label("TRACKING",m_Heading);GUILayout.Space(8);
            bool current=m_Renderer.SampleCurrent&&!WaitingForHeadset;
            Metric("Eye gaze",current&&TrialReplayRenderer.GazeUsable(sample)?"Valid ray":current?"Unavailable / invalid":"No current sample");
            Metric("Head pose",current&&TrialReplayRenderer.HeadUsable(sample)?"Tracked":"Unavailable");
            Metric("Right trigger",!current||sample?.rightController==null||sample.rightController.tracked!=1?"Unavailable":sample.rightController.triggerHeld==1?"Held":sample.rightController.triggerHeld==0?"Released":"Unknown");
            Metric("Sample age",sample==null?"Unavailable":$"{Math.Max(0,m_Time-sample.time)*1000:F0} ms");
            GUILayout.Space(12);GUILayout.Label("AGENT SPEECH",m_Heading);GUILayout.Space(8);
            var speech=m_Audio.Plan.Segments.LastOrDefault(s=>s.Contains(m_Time,m_Sequence)&&!(s.Start.detail??"").Contains("software_one_shot_start"));
            GUILayout.Label(WaitingForHeadset?"Waiting for signal":speech==null?"No saved speech at this time":string.IsNullOrEmpty(speech.Start.text)?"Recorded audio (no transcript)":speech.Start.text,m_Label);
            if(speech!=null&&!WaitingForHeadset&&Button("Play from this message")){m_Speed=1;Seek(speech.Start.time,speech.Start.sequence);m_Playing=true;Sync(true);}
            GUILayout.Space(18);GUILayout.Label("GAZE PROVENANCE",m_Heading);GUILayout.Space(8);
            Metric("Source",sample?.gazeRaySource??sample?.gazeSource);
            Metric("Alignment",sample?.gazeRayVersion==TrialReplayGaze.Version?(sample.gazeAlignmentApplied?"Applied during capture":"Not applied"):m_Correction.Supported?"Legacy display correction":"Unverified legacy ray");
            GUILayout.Label("Ray intersections are not validated fixations. Body, chair and room are reconstruction aids.",m_MutedLabel);
            GUILayout.Space(14);m_RawExpanded=GUILayout.Toggle(m_RawExpanded,"Show raw observation",m_Chip);
            if(m_RawExpanded&&sample!=null){GUILayout.Label($"Source line {m_Timeline.SampleSourceLine} · historical values remain during gaps",m_MutedLabel);GUILayout.TextArea(JsonUtility.ToJson(sample,true),m_Code);}
            if(!string.IsNullOrEmpty(m_Timeline.Warning))GUILayout.Label(m_Timeline.Warning,m_MutedLabel);
            if(!string.IsNullOrEmpty(m_Audio.Warning))GUILayout.Label(m_Audio.Warning,m_MutedLabel);
            GUILayout.EndScrollView();
        }
        GUILayout.EndArea();
    }
    void JumpEvent(int direction)
    {
        long cursor=m_Sequence==long.MaxValue?m_Timeline.CurrentRecord?.sequence??-1:m_Sequence;
        var record=direction>0?m_Events.FirstOrDefault(r=>r.sequence>cursor):m_Events.LastOrDefault(r=>r.sequence<cursor);
        if(record!=null){m_Playing=false;Seek(record.time,record.sequence);}
    }
    void Transport(Rect rect)
    {
        Panel(rect);bool enabled=GUI.enabled;GUI.enabled=enabled&&m_Timeline!=null;
        float x=rect.x+10,y=rect.y+8;
        if(Action(new Rect(x,y,68,38),m_Playing?"Pause":"Play",m_Playing)){m_FollowLive=false;m_Playing=!m_Playing;Sync(true);}
        if(Action(new Rect(x+76,y,70,38),"‹ Event"))JumpEvent(-1);
        if(Action(new Rect(x+152,y,70,38),"Event ›"))JumpEvent(1);
        double start=0,end=m_Timeline?.Duration??1;
        var trial=m_Timeline?.State.Trial;var track=m_Tracks.For(trial);
        if(!m_RunTimeline&&track!=null){start=trial.time;end=Math.Max(start+.001,track.End);}
        if(m_TimelineDrag){start=m_DragStart;end=m_DragEnd;trial=m_DragTrial;}
        GUI.Label(new Rect(x+236,y+7,230,25),$"{Clock(m_Time-start)} / {Clock(end-start)}",m_Label);
        if(Action(new Rect(x+457,y,74,38),$"{m_Speed:0.##}×"))
        {float[] speeds={.25f,.5f,1,2};m_Speed=speeds[(Array.IndexOf(speeds,m_Speed)+1)%speeds.Length];m_FollowLive=false;Sync(true);}
        if(Action(new Rect(x+541,y,70,38),m_Muted?"Unmute":"Mute",m_Muted)){m_Muted=!m_Muted;Sync(true);}
        float volume=GUI.HorizontalSlider(new Rect(x+623,y+14,95,20),m_Volume,0,1);
        if(volume!=m_Volume){m_Volume=volume;AudioListener.volume=volume;}
        if(Action(new Rect(x+734,y,85,38),"‹ Sample"))Step(-1);
        if(Action(new Rect(x+825,y,85,38),"Sample ›"))Step(1);
        if(Action(new Rect(rect.xMax-350,y,130,38),m_RunTimeline?"Whole session":"Selected trial",m_RunTimeline))m_RunTimeline=!m_RunTimeline;
        GUI.enabled=enabled&&trial!=null&&!m_ViewLive;
        if(Action(new Rect(rect.xMax-210,y,198,38),"Export trial data…"))ExportTrial();
        GUI.enabled=enabled&&m_Timeline!=null;
        var area=new Rect(rect.x+126,y+59,rect.width-144,108);
        if(m_Timeline!=null)TimelineTracks(area,start,end,trial);
        for(int i=0;i<3;i++)GUI.Label(new Rect(rect.x+14,area.y+18+i*30,110,24),new[]{"Agent speech","Gaze samples","Events"}[i],m_MutedLabel);
        string notice=string.IsNullOrEmpty(m_ExportNotice)?m_Speed==1?"Click / drag the ruler to scrub · gaze shows usable samples, not fixations · Left / Right: sample · Space: play":"Audio plays at 1× only":m_ExportNotice;
        GUI.Label(new Rect(rect.x+14,rect.yMax-24,rect.width-28,20),notice,m_MutedLabel);
        GUI.enabled=enabled;
        var e=Event.current;
        if(enabled&&m_Timeline!=null&&GUIUtility.keyboardControl==0&&e.type==EventType.KeyDown)
        {
            if(e.keyCode==KeyCode.Space){m_FollowLive=false;m_Playing=!m_Playing;Sync(true);e.Use();}
            else if(e.keyCode==KeyCode.LeftArrow){Step(-1);e.Use();}
            else if(e.keyCode==KeyCode.RightArrow){Step(1);e.Use();}
        }
    }
    void TimelineTracks(Rect rect,double start,double end,TrialReplayRecord trial)
    {
        double duration=Math.Max(.001,end-start);
        float X(double time)=>rect.x+(float)((time-start)/duration)*rect.width;
        // Roughly ten ticks, rounded to useful seconds at each range.
        double step=duration<=10?1:duration<=50?5:duration<=100?10:duration<=300?30:duration<=900?60:300;
        for(double t=0;t<=duration;t+=step)
        {float x=X(start+t);Fill(new Rect(x,rect.y+14,1,96),m_Border);GUI.Label(new Rect(x+3,rect.y-4,65,20),$"{t:0}s",m_Small);}
        for(int lane=0;lane<3;lane++)Fill(new Rect(rect.x,rect.y+18+lane*30,rect.width,26),new Color(.065f,.08f,.10f));
        foreach(var segment in m_Audio.Plan.Segments)
        {
            if(segment.End<=start||segment.Start.time>end)continue;
            float left=Mathf.Max(rect.x,X(segment.Start.time)),right=Mathf.Min(rect.xMax,X(segment.End));
            if(right<=left)continue;
            var box=new Rect(left,rect.y+19,Mathf.Max(2,right-left),24);Fill(box,new Color(.15f,.43f,.43f));
            if(GUI.Button(box,new GUIContent(segment.Start.text??"Audio",$"{EventName(segment.Start.kind)} · {segment.Start.text}"),m_Small))
            {m_Playing=false;Seek(segment.Start.time,segment.Start.sequence);}
        }
        foreach(var item in m_Tracks.Trials.Values)
        {
            if(!m_RunTimeline&&item.Snapshot!=trial)continue;
            foreach(var gaze in item.Gaze)
            {
                double a=Math.Max(start,gaze.Start),b=Math.Min(end,gaze.End);if(b<=a)continue;
                Fill(new Rect(X(a),rect.y+56,Mathf.Max(1,X(b)-X(a)),6),m_Orange);
            }
            float lastLabel=-1000;
            foreach(var record in item.Events)
            {
                if(record.time<start||record.time>end||record.kind.StartsWith("audio_",StringComparison.Ordinal)||record.kind.StartsWith("readiness_",StringComparison.Ordinal))continue;
                float x=X(record.time);Fill(new Rect(x,rect.y+83,3,12),m_Accent);
                string label=x-lastLabel>125?EventName(record.kind):"";
                if(label.Length>0)lastLabel=x;
                if(GUI.Button(new Rect(x-3,rect.y+79,Mathf.Min(label.Length>0?122:8,rect.xMax-x+3),22),new GUIContent("  "+label,$"{Clock(record.time-start)} · {EventName(record.kind)}"),m_Small))
                {m_Playing=false;Seek(record.time,record.sequence);}
            }
        }
        float cursor=Mathf.Clamp(X(m_Time),rect.x,rect.xMax);
        Fill(new Rect(cursor,rect.y+12,2,99),m_Accent);
        int id=GUIUtility.GetControlID(0x74726961,FocusType.Passive,rect);var e=Event.current;
        // Dragging uses the range captured on mouse-down even when the cursor crosses a trial boundary.
        if(GUI.enabled&&e.type==EventType.MouseDown&&e.button==0&&new Rect(rect.x,rect.y,rect.width,18).Contains(e.mousePosition))
        {m_TimelineDrag=true;m_DragStart=start;m_DragEnd=end;m_DragTrial=trial;GUIUtility.hotControl=id;}
        if(m_TimelineDrag&&GUIUtility.hotControl==id&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag||e.type==EventType.MouseUp))
        {
            m_Playing=false;Seek(TrialReviewTracks.AtFraction(m_DragStart,m_DragEnd,(e.mousePosition.x-rect.x)/rect.width));
            if(e.type==EventType.MouseUp){m_TimelineDrag=false;GUIUtility.hotControl=0;}e.Use();
        }
    }
    void ExportTrial()
    {
        try
        {
            string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Documents","Trial Review Exports");
            string folder=TrialReviewExport.Write(root,m_Timeline,m_Timeline.State.Trial,m_Hash,m_Summary?.For(m_Timeline.State.Trial));
            m_ExportNotice="Exported trial observations and available summary · "+Path.GetFileName(folder);
            Application.OpenURL(new Uri(folder).AbsoluteUri);
        }
        catch(Exception error){m_Error="Export failed: "+error.Message;}
    }
}
