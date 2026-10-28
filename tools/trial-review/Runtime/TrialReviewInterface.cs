using System;
using System.IO;
using System.Linq;
using UnityEngine;

// Desktop presentation only: recorded poses, audio and timing stay owned by the replay classes.
public sealed partial class TrialReviewApp
{
    readonly TrialReviewTracks m_Tracks = new TrialReviewTracks();
    Color m_Background, m_Surface, m_Border, m_HeadingColor, m_TrackBackground, m_SpeechBackground;
    readonly Color m_Accent=new Color(1,201f/255f,4f/255f), m_Orange=new Color(1,.59f,.25f);
    bool m_LightTheme, m_ThemeLoaded;
    GUIStyle m_Center, m_Small, m_Chip, m_ActiveChip, m_Row, m_ActiveRow;
    GUIStyle m_ViewToggleOff, m_ViewToggleOn, m_Subtitle;
    bool m_ShowSubtitles = true;
    string m_TrialQuery="", m_ExportNotice="";
    int m_TrialFilter, m_CameraMode;
    bool m_RawExpanded, m_RunTimeline = true, m_TimelineDrag;
    float m_Volume=.8f;
    double m_DragStart, m_DragEnd;
    int m_DragControl;
    bool m_SpaceHeld;
    TrialReplayRecord m_DragTrial;
    Texture2D m_ButtonTexture, m_SelectedTexture, m_HoverTexture;
    Texture2D m_GazeDotTexture;
    TrialReviewWaitingPyramid m_WaitingPyramid;
    Texture2D m_UcfLogo;

    Texture2D Solid(Color color)
    { var texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();return texture; }
    void Styles()
    {
        if(m_Ready)return;m_Ready=true;
        if(!m_ThemeLoaded)
        {
            m_ThemeLoaded=true;m_LightTheme=PlayerPrefs.GetInt("ViewerLightTheme",0)==1;
            var args=Environment.GetCommandLineArgs();int theme=Array.IndexOf(args,"--theme");
            if(theme>=0&&theme+1<args.Length&&(args[theme+1]=="light"||args[theme+1]=="dark"))m_LightTheme=args[theme+1]=="light";
        }
        foreach(var texture in new[]{m_Panel,m_ButtonTexture,m_SelectedTexture,m_HoverTexture})if(texture!=null)Destroy(texture);
        m_Background=m_LightTheme?new Color(.94f,.94f,.92f):new Color(.045f,.045f,.045f);
        m_Surface=m_LightTheme?new Color(.99f,.985f,.97f):new Color(.085f,.085f,.08f);
        m_Border=m_LightTheme?new Color(.76f,.75f,.71f):new Color(.25f,.24f,.20f);
        m_HeadingColor=m_LightTheme?new Color(.35f,.34f,.31f):new Color(.70f,.69f,.64f);
        m_TrackBackground=m_LightTheme?new Color(.89f,.88f,.84f):new Color(.065f,.065f,.06f);
        m_SpeechBackground=m_LightTheme?new Color(.90f,.79f,.44f):new Color(.34f,.28f,.10f);
        Color text=m_LightTheme?new Color(.09f,.09f,.08f):new Color(.95f,.95f,.93f);
        Color muted=m_LightTheme?new Color(.35f,.34f,.31f):new Color(.70f,.69f,.64f);
        m_Panel=Solid(m_Surface);m_ButtonTexture=Rounded(m_LightTheme?new Color(.89f,.88f,.84f):new Color(.14f,.14f,.125f));
        m_SelectedTexture=Rounded(m_Accent);m_HoverTexture=Rounded(m_LightTheme?new Color(.93f,.93f,.91f):new Color(.18f,.18f,.17f));
        m_Label=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,richText=false,normal={textColor=text}};
        m_MutedLabel=new GUIStyle(m_Label){fontSize=12,normal={textColor=muted}};
        m_Subtitle=new GUIStyle(m_Label){fontSize=22,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
        m_Title=new GUIStyle(m_Label){fontSize=20,fontStyle=FontStyle.Bold};
        m_Heading=new GUIStyle(m_Label){fontSize=11,fontStyle=FontStyle.Bold,normal={textColor=m_HeadingColor}};
        m_Center=new GUIStyle(m_Label){alignment=TextAnchor.MiddleCenter,wordWrap=false};
        m_Small=new GUIStyle(m_MutedLabel){alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip};
        m_Button=new GUIStyle(GUI.skin.button){fontSize=14,richText=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,8,8),border=new RectOffset(6,6,6,6)};
        m_Button.normal.background=m_ButtonTexture;m_Button.normal.textColor=m_Label.normal.textColor;
        m_Button.hover.background=m_HoverTexture;m_Button.hover.textColor=text;
        m_Button.active.background=m_SelectedTexture;m_Button.active.textColor=Color.black;
        m_Button.focused.background=m_HoverTexture;m_Button.focused.textColor=text;
        foreach(var state in new[]{m_Button.onNormal,m_Button.onHover,m_Button.onActive,m_Button.onFocused})
        {state.background=m_SelectedTexture;state.textColor=Color.black;}
        m_Chip=new GUIStyle(m_Button){fontSize=13,padding=new RectOffset(8,8,8,8)};
        m_ActiveChip=new GUIStyle(m_Chip);m_ActiveChip.normal.background=m_SelectedTexture;m_ActiveChip.normal.textColor=Color.black;
        m_ActiveChip.hover.background=m_SelectedTexture;m_ActiveChip.hover.textColor=Color.black;
        m_ActiveChip.focused.background=m_SelectedTexture;m_ActiveChip.focused.textColor=Color.black;
        // Independent view options use radio-like indicators, not gold selection blocks.
        m_ViewToggleOff=new GUIStyle(m_Chip){richText=true,padding=new RectOffset(8,8,2,2)};
        foreach(var state in new[]{m_ViewToggleOff.normal,m_ViewToggleOff.hover,m_ViewToggleOff.active,m_ViewToggleOff.focused})
        {state.background=m_ButtonTexture;state.textColor=text;}
        m_ViewToggleOn=new GUIStyle(m_ViewToggleOff){fontStyle=FontStyle.Bold};
        m_Row=new GUIStyle(m_Button){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=14,padding=new RectOffset(12,12,8,8)};
        m_ActiveRow=new GUIStyle(m_Row);m_ActiveRow.normal.background=m_HoverTexture;m_ActiveRow.normal.textColor=m_HeadingColor;
        m_Field=new GUIStyle(GUI.skin.textField){fontSize=14,padding=new RectOffset(8,8,8,8),border=new RectOffset(6,6,6,6),normal={textColor=text,background=m_ButtonTexture}};
        m_Field.focused.background=m_HoverTexture;m_Field.focused.textColor=text;
        m_Field.hover.background=m_ButtonTexture;m_Field.hover.textColor=text;
        m_Field.active.background=m_HoverTexture;m_Field.active.textColor=text;
        m_Code=new GUIStyle(m_MutedLabel){fontSize=11,wordWrap=true};
        m_WaitingTitle=new GUIStyle(m_Heading){alignment=TextAnchor.MiddleCenter};
        m_DegreeLabel=new GUIStyle(m_Heading){fontSize=13,alignment=TextAnchor.MiddleCenter,wordWrap=false};
        m_DegreeLabel.normal.textColor=new Color(.9f,.92f,.94f); // Scene labels always sit on a dark backing.
        GUI.skin.settings.cursorColor=text;GUI.skin.settings.selectionColor=new Color(.70f,.55f,.10f,.65f);
        DesignTokens();
        AudioListener.volume=m_Muted?0:m_Volume;
    }
    void SetViewerAudio(float volume, bool muted)
    {
        m_Volume=Mathf.Clamp01(volume);m_Muted=muted;
        AudioListener.volume=m_Muted?0:m_Volume;
        if(!m_DetachedReview)
        {
            PlayerPrefs.SetFloat("ViewerVoiceVolume",m_Volume);
            PlayerPrefs.SetInt("ViewerVoiceMuted",m_Muted?1:0);
            PlayerPrefs.Save();
        }
        // Do not seek/restart a sentence when muting or changing gain.
    }
    void ToggleTheme()
    {
        m_LightTheme=!m_LightTheme;m_Ready=false;
        PlayerPrefs.SetInt("ViewerLightTheme",m_LightTheme?1:0);PlayerPrefs.Save();
    }
    bool Button(string label,params GUILayoutOption[] options)=>GUILayout.Button(HelpContent(label),m_Button,options);
    bool Action(Rect rect,string label,bool active=false)=>GUI.Button(rect,HelpContent(label),active?m_ActiveChip:m_Chip);
    void Fill(Rect rect,Color color)=>GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,color,0,0);
    void Panel(Rect rect)=>Fill(rect,m_Surface);
    static string Clock(double seconds)
    { seconds=Math.Max(0,seconds);return $"{(int)(seconds/60):00}:{seconds%60:00.00}"; }
    static string Voice(string voice)=>string.IsNullOrEmpty(voice)?"Unknown voice":voice.Replace('_',' ');
    static string EventName(string kind)=>string.IsNullOrEmpty(kind)?"Event":kind.Replace('_',' ');

    void OnGUI()
    {
        if(m_CaptureMode && Event.current.type!=EventType.Layout && Event.current.type!=EventType.Repaint) return;
        Styles();
        if(m_CaptureMode&&m_CaptureCheckInputs&&m_Timeline!=null&&Event.current.type==EventType.Repaint){RunCaptureInputChecks();return;}
        // Keep controls comfortably sized on both retina and small desktop windows.
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float width=Screen.width/scale,height=Screen.height/scale;
        Fill(new Rect(0,0,width,height),m_Background);
        GUI.enabled=!m_Browser&&!m_Loading&&!m_DatasetPanel&&!m_IntakeOpen&&!m_ValidationOpen;
        HandlePlaybackKeyboard(Event.current, GUI.enabled, GUI.GetNameOfFocusedControl());
        Header(width);
        if(m_LivePanel)LiveControls(width);
        float top=m_LivePanel?78+LiveControlsHeight:78,bottom=height-260,rail=204,inspector=380;
        Panel(new Rect(0,top,rail,bottom-top));Panel(new Rect(width-inspector,top,inspector,bottom-top));
        float left=rail+14,viewWidth=width-inspector-14-left;
        m_OptionColumns=Mathf.Clamp(Mathf.FloorToInt(viewWidth/145),3,8);
        float optionsHeight=8;
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
            if(WaitingForHeadset)DrawWaitingConnection();
            else
            {
                m_WaitingTitle.fontSize=Mathf.RoundToInt(Mathf.Clamp(m_Viewport.width*.047f,30,56));
                var title=new GUIContent("Open a run to begin");
                float titleHeight=m_WaitingTitle.CalcHeight(title,m_Viewport.width-48);
                GUI.Label(new Rect(m_Viewport.x+24,m_Viewport.center.y-titleHeight/2,m_Viewport.width-48,titleHeight),title,m_WaitingTitle);
            }
        }
        Transport(new Rect(14,bottom+12,width-28,238));
        if(!string.IsNullOrEmpty(m_Error))
        {
            Panel(new Rect(left,top+50,viewWidth,64));
            GUI.Label(new Rect(left+12,top+56,viewWidth-24,52),m_Error,m_Label);
        }
        HeaderMenu(width);
        GUI.enabled=true;
        if(m_Loading){Panel(new Rect(width/2-190,height/2-40,380,80));GUI.Label(new Rect(width/2-180,height/2-30,360,60),"Loading recording…",m_Center);}
        if(m_Browser)Browser(new Rect(width/2-380,80,760,height-160));
        if(m_DatasetPanel)DatasetDialog(width,height);
        DrawHoverHelp(width,height);
        DrawIntake(width,height);
        DrawValidation(width,height);
    }
    void DrawWaitingConnection()
    {
        float size=Mathf.Min(220,m_Viewport.height*.57f,m_Viewport.width*.48f);
        float top=m_Viewport.center.y-(size+38)/2;
        if(Event.current.type==EventType.Repaint)
        {
            m_WaitingPyramid??=new TrialReviewWaitingPyramid();
            GUI.DrawTexture(new Rect(m_Viewport.center.x-size/2,top,size,size),
                m_WaitingPyramid.Render(Time.realtimeSinceStartupAsDouble),ScaleMode.ScaleToFit,true);
        }
        m_WaitingTitle.fontSize=24;
        GUI.Label(new Rect(m_Viewport.x+12,top+size+6,m_Viewport.width-24,32),LiveWaitingReason,m_WaitingTitle);
        if(HeadsetPreparing)
        {
            float barWidth=Mathf.Min(360,m_Viewport.width-48);
            var bar=new Rect(m_Viewport.center.x-barWidth/2,top+size+46,barWidth,12);
            Fill(bar,new Color(.2f,.2f,.2f));
            var loading=m_LiveStatus.headsetState;
            if(loading.progressKnown)
            {
                Fill(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(loading.progress01),bar.height),m_Accent);
                GUI.Label(new Rect(bar.x,bar.yMax+4,bar.width,24),$"{Mathf.RoundToInt(loading.progress01*100)}%",m_WaitingTitle);
            }
            else GUI.Label(new Rect(bar.x,bar.yMax+4,bar.width,24),"Progress not reported",m_WaitingTitle);
        }
        m_UcfLogo??=Resources.Load<Texture2D>("UcfLogo");
        if(m_UcfLogo!=null)GUI.DrawTexture(new Rect(m_Viewport.xMax-102,m_Viewport.yMax-70,84,56),m_UcfLogo,ScaleMode.ScaleToFit,true);
    }
    bool m_FileMenu;
    void Header(float width)
    {
        Panel(new Rect(0,0,width,64));Fill(new Rect(0,63,width,1),m_Border);
        string participant=AssignedParticipantId;
        if(string.IsNullOrEmpty(participant))participant=m_Timeline?.State.Trial?.participantCode;
        if(!string.IsNullOrEmpty(m_ParticipantName))participant=m_ParticipantName;
        int run=m_Timeline?.State.Trial?.runNumber??m_Timeline?.Header.runNumber??0;
        string name=m_Timeline==null?"Choose a session":$"{(string.IsNullOrEmpty(participant)?"Recorded session":participant)} · {(run>0?$"Run {run}":"Session")}";
        if(!string.IsNullOrEmpty(DatasetName))name=DatasetName;
        string source=m_Timeline?.Header.sourceType??"";
        string provenance=source.IndexOf("synthetic",StringComparison.OrdinalIgnoreCase)>=0?"SYNTHETIC":source.IndexOf("development",StringComparison.OrdinalIgnoreCase)>=0?"DEVELOPMENT":null;
        float left=16;
        if(provenance!=null){StatusChip(new Rect(left,18,112,28),provenance,m_Warn,true);left+=120;}
        var breadcrumb=new Rect(left,14,232,36);
        if(GUI.Button(breadcrumb,new GUIContent((participant==provenance&&string.IsNullOrEmpty(DatasetName)?$"Run {run}":name)+" ▾",name+" · Participants & datasets"),m_Chip))OpenDatasetPanel();
        string status=m_FollowLive?(WaitingForHeadset?"Waiting for signal":m_DrainingLive?"Buffered replay":"Following live"):m_ViewLive?"Reviewing archive":"Saved replay";
        Color stateColor=m_FollowLive?(WaitingForHeadset?m_MutedLabel.normal.textColor:m_DrainingLive?m_Warn:m_Ok):m_ViewLive?m_Info:m_MutedLabel.normal.textColor;
        if(WaitingForHeadset)stateColor.a=.55f+.45f*Mathf.Abs(Mathf.Sin(Time.unscaledTime*2));
        var statusRect=new Rect(left+244,18,154,28);
        StatusChip(statusRect,"",stateColor);
        GUI.Label(new Rect(statusRect.x+10,statusRect.y,14,statusRect.height),"●",new GUIStyle(m_Small){normal={textColor=stateColor}});
        GUI.Label(new Rect(statusRect.x+28,statusRect.y,statusRect.width-32,statusRect.height),status,new GUIStyle(m_Label){fontSize=12,alignment=TextAnchor.MiddleLeft});
        float right=width-688;
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!m_DetachedReview;
        if(Action(new Rect(right,14,208,36),"New participant session",true))OpenIntake();
        GUI.enabled=enabled;
        if(Action(new Rect(right+216,14,76,36),"File ▾",m_FileMenu))m_FileMenu=!m_FileMenu;
        if(m_DetachedReview)GUI.Label(new Rect(right+304,14,312,36),"Detached review · live stays in main window",m_MutedLabel);
        else
        {
            if(Action(new Rect(right+300,14,148,36),"Wi-Fi settings",m_LivePanel))m_LivePanel=!m_LivePanel;
            LiveModeSwitch(new Rect(right+456,14,160,36));
        }
        if(GUI.Button(new Rect(width-64,14,48,36),new GUIContent(m_LightTheme?"Dark":"Light",m_LightTheme?"Dark mode":"Light mode"),m_Chip))ToggleTheme();
    }
    void HeaderMenu(float width)
    {
        if(!m_FileMenu)return;
        var rect=new Rect(width-472,58,208,100);Fill(rect,m_Border);Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),m_Raised);
        if(Action(new Rect(rect.x+8,rect.y+8,192,36),"Open run…")){m_FileMenu=false;Browse(false);}
        if(Action(new Rect(rect.x+8,rect.y+52,192,36),"Demo")){m_FileMenu=false;StartCoroutine(Open(Path.Combine(Application.streamingAssetsPath,"Demo/recording.jsonl")));}
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){m_FileMenu=false;Event.current.Use();}
        if(Event.current.type==EventType.MouseDown&&!rect.Contains(Event.current.mousePosition)&&!new Rect(width-472,14,76,36).Contains(Event.current.mousePosition))m_FileMenu=false;
    }
    void LiveModeSwitch(Rect rect)
    {
        // The full label/track is one keyboard-focusable control; position conveys state as well as color.
        GUI.SetNextControlName("live-mode-switch");
        if(GUI.Button(rect,new GUIContent("",Help("Follow live")),m_ViewToggleOff))SetLiveMode(!m_FollowLive);
        var label=new GUIStyle(m_Label){fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft};
        GUI.Label(new Rect(rect.x+10,rect.y,86,rect.height),"Live mode",label);
        var track=new Rect(rect.xMax-52,rect.center.y-12,44,24);
        GUI.DrawTexture(track,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,m_FollowLive?m_Accent:m_Border,0,12);
        var thumb=new Rect(m_FollowLive?track.xMax-21:track.x+3,track.y+3,18,18);
        GUI.DrawTexture(thumb,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,m_FollowLive?Color.black:m_Label.normal.textColor,0,9);
    }
    bool m_LayersOpen;
    Rect m_LayersRect;
    void CameraControls(Rect rect)
    {
        string[] names={"First person","Third person","Overhead"};
        for(int i=0;i<3;i++)
        {
            var segment=new Rect(rect.x+i*124,rect.y,120,36);
            bool fallback=i==0&&m_CameraMode==0&&m_Renderer!=null&&!m_Renderer.UsingRecordedHead;
            if(fallback)
            {
                StatusChip(segment,names[i],m_Warn,true);
                if(GUI.Button(segment,new GUIContent("",Help(names[i])+" Head pose unavailable · overview shown"),GUIStyle.none))m_CameraMode=i;
            }
            else if(Action(segment,names[i],m_CameraMode==i))m_CameraMode=i;
        }
        if(Action(new Rect(rect.xMax-252,rect.y,92,36),"Reset view"))ResetCamera();
        int count=new[]{m_ShowEyeRay,m_Renderer?.ShowControllers??false,m_ShowBody,m_ShowWalls,m_ShowDegrees,m_ShowRoom,m_Renderer?.ShowTarget??false,m_Renderer?.ShowGaze??false,m_ShowDetectionRing,m_ShowSubtitles,m_ShowStateMarkers}.Count(value=>value);
        if(Action(new Rect(rect.xMax-152,rect.y,152,36),$"Layers · {count} on",m_LayersOpen))m_LayersOpen=!m_LayersOpen;
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
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("text:trial-search");
        m_TrialQuery=GUILayout.TextField(m_TrialQuery,m_Field,GUILayout.Height(36));
        var search=GUILayoutUtility.GetLastRect();
        if(string.IsNullOrEmpty(m_TrialQuery)&&GUI.GetNameOfFocusedControl()!="text:trial-search")GUI.Label(new Rect(search.x+8,search.y,search.width-16,search.height),"Search trials…",m_Small);
        if(GUILayout.Button(new GUIContent("×","Clear trial search"),m_Chip,GUILayout.Width(28),GUILayout.Height(36)))m_TrialQuery="";
        GUILayout.EndHorizontal();
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
            if(next!=group)
            {
                GUILayout.Space(12);GUILayout.Label(new GUIContent(trial.practice?"PRACTICE":$"BLOCK {trial.block}",$"Recorded block: {trial.block} · voice: {trial.voice}"),m_Heading);
                var voiceRect=GUILayoutUtility.GetRect(10,24,GUILayout.ExpandWidth(true));
                StatusChip(voiceRect,Voice(trial.voice),m_Info);GUILayout.Space(8);group=next;
            }
            var track=m_Tracks.For(trial);
            string duration=track==null?"":Clock(track.End-trial.time);
            bool selected=trial==m_Timeline.State.Trial;
            var row=GUILayoutUtility.GetRect(10,76,GUILayout.ExpandWidth(true));
            var rowStyle=new GUIStyle(m_Row);if(selected)rowStyle.normal.background=m_CardTexture;
            if(GUI.Button(row,new GUIContent("",$"{next} · trial {TrialNumber(trial)} · {duration}"),rowStyle))SelectTrial(trial);
            if(selected)Fill(new Rect(row.x,row.y+6,3,row.height-12),m_Accent);
            GUI.Label(new Rect(row.x+12,row.y+8,40,24),$"{TrialNumber(trial):00}",new GUIStyle(m_Mono){fontSize=18});
            GUI.Label(new Rect(row.x+50,row.y+12,row.width-62,20),duration,new GUIStyle(m_Mono){fontSize=12,alignment=TextAnchor.MiddleRight});
            GUI.Label(new Rect(row.x+12,row.y+43,70,22),$"|θ| {trial.absoluteTheta}°",new GUIStyle(m_Label){fontSize=12});
            StatusChip(new Rect(row.xMax-82,row.y+42,74,24),track?.Completed==true?"Selected":track?.TimedOut==true?"Time up":"Recorded",track?.Completed==true?m_Ok:track?.TimedOut==true?m_Warn:m_MutedLabel.normal.textColor);
            GUILayout.Space(8);
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
        m_GuidanceState.Prepare(m_Timeline,m_Time,m_Sequence,WaitingForHeadset);
        m_Environment.Prepare(m_Timeline.State.Trial,m_ShowWalls,m_ShowRoom);
        m_Actors.Prepare(m_Timeline,m_Time,m_Renderer.RecordedHead,m_ShowBody,m_Renderer.ShowControllers,m_ShowEyeRay,m_Correction);
        if(m_Actors.RightVisible)m_Renderer.AttachControllerRay(m_Actors.RightControllerTip);
        m_Vicinity.Prepare(m_Timeline,m_Renderer,m_Time,m_Sequence,m_Actors.GazeAvailable&&!WaitingForHeadset,m_Actors.GazeRay,m_ShowDetectionRing,m_GuidanceState.Current);
        // The orbit follows the fixed seated center, never per-frame head translation.
        if(!m_Renderer.RecordedHead)
            m_Renderer.Camera.transform.position+=m_Actors.ViewCenter;
        if(Event.current.type==EventType.Repaint)
        {
            Vector3 gazePoint=default;
            bool showDot=false;
            if(m_Renderer.UsingRecordedHead&&m_Actors.EyeRay.enabled)
            {
                Vector3 origin=m_Actors.EyeRay.GetPosition(0);
                showDot=m_Environment.TryProjectGaze(new Ray(origin,m_Actors.EyeRay.GetPosition(1)-origin),out gazePoint);
                m_Actors.EyeRay.enabled=false;
            }
            GUI.DrawTexture(m_Viewport,m_Renderer.Render(m_Viewport),ScaleMode.StretchToFill,false);
            if(showDot)DrawGazeDot(gazePoint);
        }
        DrawDegrees();
        DrawStateMarkers();
        DrawSubtitles();
        if(m_Renderer.RecordedHead&&!m_Renderer.UsingRecordedHead)
        {
            var banner=new Rect(m_Viewport.x+12,m_Viewport.y+12,Mathf.Min(460,m_Viewport.width-24),60);
            Fill(banner,new Color(.15f,.09f,.025f,.94f));
            GUI.Label(new Rect(banner.x+12,banner.y+8,banner.width-24,24),"Head pose unavailable · overview shown",new GUIStyle(m_Label){normal={textColor=new Color32(255,159,67,255)}});
            GUI.Label(new Rect(banner.x+12,banner.y+32,banner.width-24,20),"at Study "+Clock(m_Time),new GUIStyle(m_Mono){fontSize=12,normal={textColor=new Color(.85f,.85f,.85f)}});
        }
        var caption=new Rect(m_Viewport.x,m_Viewport.yMax-30,m_Viewport.width,30);
        Fill(caption,new Color(.025f,.03f,.035f,.9f));
        GUI.Label(new Rect(caption.x+12,caption.y+4,caption.width-24,24),
            $"{(m_Vicinity.RecordedWinner?"Recorded highlight":"Reconstructed highlight (not recorded)")} · ring estimated · padding {m_Vicinity.Padding:0.#}° ({(m_Vicinity.RecordedPadding?"recorded":"default")})",new GUIStyle(m_Small){normal={textColor=new Color(.8f,.8f,.77f)}});
        DrawLayers(recordedGaze);
        var e=Event.current;
        if(GUI.enabled&&m_Viewport.Contains(e.mousePosition)&&!(m_LayersOpen&&m_LayersRect.Contains(e.mousePosition))&&!m_Renderer.RecordedHead&&!WaitingForHeadset)
        {
            if(e.type==EventType.MouseDrag&&e.button==1){m_Renderer.Yaw+=e.delta.x;m_Renderer.Pitch=Mathf.Clamp(m_Renderer.Pitch+e.delta.y,-85,85);e.Use();}
            if(e.type==EventType.ScrollWheel){m_Renderer.Distance=Mathf.Clamp(m_Renderer.Distance+e.delta.y*.2f,.3f,30);e.Use();}
        }
    }
    void DrawSubtitles()
    {
        if(!m_ShowSubtitles || WaitingForHeadset)return;
        string text=m_Tracks.Subtitles.At(m_Time,m_Sequence);
        if(string.IsNullOrEmpty(text))return;
        float width=Mathf.Min(m_Viewport.width-32,760);
        m_Subtitle.fontSize=Mathf.RoundToInt(Mathf.Clamp(width/28,16,24));
        var content=new GUIContent(text);
        float height=m_Subtitle.CalcHeight(content,width-28)+20;
        var box=new Rect(m_Viewport.center.x-width/2,m_Viewport.yMax-height-42,width,height);
        Fill(box,new Color(0,0,0,.86f));
        GUI.Label(new Rect(box.x+14,box.y+10,box.width-28,box.height-20),content,m_Subtitle);
    }
    void DrawGazeDot(Vector3 point)
    {
        Vector3 screen=m_Renderer.Camera.WorldToViewportPoint(point);
        if(screen.z<=m_Renderer.Camera.nearClipPlane||screen.x<0||screen.x>1||screen.y<0||screen.y>1)return;
        if(m_GazeDotTexture==null)
        {
            m_GazeDotTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);
            m_GazeDotTexture.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                m_GazeDotTexture.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(15.5f-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16)))));
            m_GazeDotTexture.Apply();
        }
        float xPos=m_Viewport.x+screen.x*m_Viewport.width,yPos=m_Viewport.y+(1-screen.y)*m_Viewport.height;
        Color previous=GUI.color;
        GUI.color=new Color(.03f,.04f,.05f);GUI.DrawTexture(new Rect(xPos-8,yPos-8,16,16),m_GazeDotTexture);
        GUI.color=new Color(1,.65f,.12f);GUI.DrawTexture(new Rect(xPos-5,yPos-5,10,10),m_GazeDotTexture);
        GUI.color=previous;
    }
    void DrawLayers(bool recordedGaze)
    {
        if(!m_LayersOpen)return;
        m_LayersRect=new Rect(m_Viewport.xMax-272,m_Viewport.y+4,268,Mathf.Min(468,m_Viewport.height-38));
        Panel(m_LayersRect);
        GUILayout.BeginArea(new Rect(m_LayersRect.x+12,m_LayersRect.y+12,m_LayersRect.width-24,m_LayersRect.height-24));
        GUILayout.Label(new GUIContent("PARTICIPANT","3D reconstruction"),m_Heading);
        m_ShowEyeRay=LayerSwitch(m_ShowEyeRay,recordedGaze?"Eye gaze":m_Correction.Supported?"Corrected gaze":"Eye ray (display)");
        m_Renderer.ShowGaze=LayerSwitch(m_Renderer.ShowGaze,recordedGaze?"Before alignment":"Raw gaze");
        m_Renderer.ShowControllers=LayerSwitch(m_Renderer.ShowControllers,"Controllers");
        m_ShowBody=LayerSwitch(m_ShowBody,"Human model");
        GUILayout.Space(8);GUILayout.Label("SCENE",m_Heading);
        m_ShowWalls=LayerSwitch(m_ShowWalls,"Viewer walls");
        m_ShowRoom=LayerSwitch(m_ShowRoom,"Room");
        m_ShowDegrees=LayerSwitch(m_ShowDegrees,"Degrees");
        m_Renderer.ShowTarget=LayerSwitch(m_Renderer.ShowTarget,"Reveal target");
        GUILayout.Space(8);GUILayout.Label("OVERLAYS",m_Heading);
        m_ShowDetectionRing=LayerSwitch(m_ShowDetectionRing,"Detection ring");
        m_ShowSubtitles=LayerSwitch(m_ShowSubtitles,"Subtitles");
        m_ShowStateMarkers=LayerSwitch(m_ShowStateMarkers,"State markers");
        GUILayout.EndArea();
    }
    bool LayerSwitch(bool value,string label)
    {
        var style=new GUIStyle(m_Chip){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(8,8,4,4)};
        return GUILayout.Button(new GUIContent((value?"☑  ":"☐  ")+label,Help(label)),style,GUILayout.Height(28))?!value:value;
    }
    void Metric(string label,string value)
    {
        GUILayout.BeginHorizontal();GUILayout.Label(label,m_MutedLabel,GUILayout.Width(104));
        value=string.IsNullOrEmpty(value)?"Unavailable":value;
        bool unavailable=value.Contains("Unavailable")||value.Contains("No current sample")||value.Contains("Not recorded");
        bool numeric=value.Any(char.IsDigit)&&!value.Any(c=>char.IsLetter(c)&&c!='m'&&c!='s');
        GUILayout.Label(value,unavailable?m_Unavailable:numeric?new GUIStyle(m_Mono){wordWrap=true}:m_Label);GUILayout.EndHorizontal();GUILayout.Space(4);
    }
    void Inspector(Rect rect)
    {
        GUILayout.BeginArea(rect);GUILayout.BeginHorizontal();
        string[] tabs={"At cursor","Events","Final data","Jev","Evaluation"};
        string[] labels={"Cursor","Events","Final","Jev","Eval"};
        for(int i=0;i<tabs.Length;i++)
        {
            var style=new GUIStyle(m_Chip){fontStyle=m_Tab==i?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(4,4,8,8)};
            foreach(var part in new[]{style.normal,style.hover,style.active,style.focused}){part.background=null;part.textColor=m_Label.normal.textColor;}
            if(GUILayout.Button(new GUIContent(labels[i],Help(tabs[i])),style,GUILayout.Width((rect.width-20)/5))){m_Tab=i;m_InspectScroll=Vector2.zero;}
            var tabRect=GUILayoutUtility.GetLastRect();if(m_Tab==i)Fill(new Rect(tabRect.x,tabRect.yMax-2,tabRect.width,2),m_Accent);
        }
        GUILayout.EndHorizontal();GUILayout.Space(12);
        var state=m_Timeline.State;var trial=state.Trial;var sample=state.Sample;
        if(m_Tab==4) EvaluationInspector(trial,rect.width);
        else if(m_Tab==3) JevInspector(trial);
        else if(m_Tab==1)
        {
            m_EventScroll=GUILayout.BeginScrollView(m_EventScroll);
            var track=m_Tracks.For(trial);
            if(track!=null)foreach(var r in track.Events)
            {
                if(GUILayout.Button($"{Clock(r.time-trial.time)} · {EventName(r.kind)}\n{TrialReviewGuidanceState.Transition(r)}",m_Row))
                {m_Playing=false;Seek(r.time,r.sequence);}
            }
            GUILayout.EndScrollView();
        }
        else if(m_Tab==2)
        {
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            GUILayout.BeginVertical(m_Card);RunExportControls();GUILayout.EndVertical();
            GUILayout.BeginVertical(m_Card);GUILayout.Label("FINAL TRIAL RESULTS",m_Heading);
            GUILayout.Label("Final summary context, not values at the cursor. Block surveys repeat across trials.",m_MutedLabel);
            if(Button("Choose CSV export…"))Browse(true);
            var row=m_Summary?.For(trial);
            if(row==null)GUILayout.Label("No matching summary loaded. Choose a completed export with manifest.json and trials.csv.",m_Label);
            else foreach(var pair in row){GUILayout.Label(pair.Key,m_Heading);GUILayout.Label(string.IsNullOrEmpty(pair.Value)?"Unavailable":pair.Value,m_Label);GUILayout.Space(8);}
            GUILayout.EndVertical();GUILayout.EndScrollView();
        }
        else
        {
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            GUILayout.BeginVertical(m_Card);
            GUILayout.Label(trial==null?"No trial":$"{(trial.practice?"Practice":"Trial")} {TrialNumber(trial):00}",m_Title);GUILayout.Space(8);
            Metric("Voice",Voice(trial?.voice));Metric("Study time",Clock(m_Time));Metric("Trial time",Clock(m_Time-(trial?.time??0)));Metric("Search",state.Searching?"Active":"Inactive");
            GUILayout.EndVertical();
            if(BeginInspectorCard("Agent speech"))
            {
                var speech=m_Audio.Plan.Segments.LastOrDefault(s=>s.Contains(m_Time,m_Sequence)&&!(s.Start.detail??"").Contains("software_one_shot_start"));
                GUILayout.Label(WaitingForHeadset?"Waiting for signal":speech==null?"No saved speech at this time":string.IsNullOrEmpty(speech.Start.text)?"Recorded audio (no transcript)":speech.Start.text,speech==null?m_Unavailable:m_Label);
                if(speech!=null&&!WaitingForHeadset&&Button("Play from this message")){m_Speed=1;Seek(speech.Start.time,speech.Start.sequence);m_Playing=true;Sync(true);}
            }
            EndInspectorCard();
            if(BeginInspectorCard("Guidance state"))GuidanceInspector();EndInspectorCard();
            if(BeginInspectorCard("Tracking"))
            {
                bool current=m_Renderer.SampleCurrent&&!WaitingForHeadset;
                Metric("Eye gaze",current&&TrialReplayRenderer.GazeUsable(sample)?"Valid ray":current?"Unavailable / invalid":"No current sample");
                Metric("Head pose",current&&TrialReplayRenderer.HeadUsable(sample)?"Tracked":"Unavailable");
                Metric("Right trigger",!current||sample?.rightController==null||sample.rightController.tracked!=1?"Unavailable":sample.rightController.triggerHeld==1?"Held":sample.rightController.triggerHeld==0?"Released":"Unknown");
                Metric("Sample age",sample==null?"Unavailable":$"{Math.Max(0,m_Time-sample.time)*1000:F0} ms");
            }
            EndInspectorCard();
            if(BeginInspectorCard("Pre-cue angle snapshot"))ThetaInspector();EndInspectorCard();
            if(BeginInspectorCard("Gaze provenance"))
            {
                Metric("Source",sample?.gazeRaySource??sample?.gazeSource);
                Metric("Alignment",sample?.gazeRayVersion==TrialReplayGaze.Version?(sample.gazeAlignmentApplied?"Applied during capture":"Not applied"):m_Correction.Supported?"Legacy display correction":"Unverified legacy ray");
                GUILayout.Label("Ray intersections are not validated fixations. Body, chair and room are reconstruction aids.",m_MutedLabel);
            }
            EndInspectorCard();
            if(BeginInspectorCard("Raw observation"))
            {
                m_RawExpanded=GUILayout.Toggle(m_RawExpanded,"Show raw observation",m_Chip);
                if(m_RawExpanded&&sample!=null){GUILayout.Label($"Source line {m_Timeline.SampleSourceLine} · historical values remain during gaps",m_MutedLabel);GUI.SetNextControlName("text:raw-data");GUILayout.TextArea(JsonUtility.ToJson(sample,true),m_Code);}
            }
            EndInspectorCard();
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
    bool m_ShortcutsOpen;
    void Transport(Rect rect)
    {
        Panel(rect);bool enabled=GUI.enabled;GUI.enabled=enabled&&m_Timeline!=null;
        float x=rect.x+12,y=rect.y+8;
        if(Action(new Rect(x,y,68,38),"‹ Event"))JumpEvent(-1);
        if(GUI.Button(new Rect(x+76,y,40,38),new GUIContent(m_Playing?"Ⅱ":"▶",Help(m_Playing?"Pause":"Play")),new GUIStyle(m_Chip){fontSize=20,padding=new RectOffset(4,4,4,4)}))TogglePlayback();
        if(Action(new Rect(x+124,y,68,38),"Event ›"))JumpEvent(1);
        GUI.Label(new Rect(x+212,y-1,146,28),Clock(m_Time),m_Clock);
        GUI.Label(new Rect(x+212,y+27,146,20),"Trial "+Clock(m_Time-(m_Timeline?.State.Trial?.time??0)),new GUIStyle(m_Mono){fontSize=11,normal={textColor=m_MutedLabel.normal.textColor}});
        if(Action(new Rect(x+366,y,56,38),$"{m_Speed:0.##}×"))
        {float[] speeds={.25f,.5f,1,2};m_Speed=speeds[(Array.IndexOf(speeds,m_Speed)+1)%speeds.Length];m_FollowLive=false;Sync(true);}
        if(Action(new Rect(x+438,y,76,38),"‹ Sample"))Step(-1);
        if(Action(new Rect(x+522,y,76,38),"Sample ›"))Step(1);
        double start=0,end=m_Timeline?.Duration??1;
        var trial=m_Timeline?.State.Trial;var track=m_Tracks.For(trial);
        if(!m_RunTimeline&&track!=null){start=trial.time;end=Math.Max(start+.001,track.End);}
        if(m_TimelineDrag){start=m_DragStart;end=m_DragEnd;trial=m_DragTrial;}
        float right=rect.xMax;
        if(Action(new Rect(right-640,y,124,38),"Whole session",m_RunTimeline))m_RunTimeline=true;
        if(Action(new Rect(right-512,y,124,38),"Selected trial",!m_RunTimeline))m_RunTimeline=false;
        GUI.enabled=enabled&&trial!=null&&!m_ViewLive;
        if(Action(new Rect(right-372,y,164,38),"Export trial data…"))ExportTrial();
        GUI.enabled=enabled;
        var speaker=new Rect(right-192,y,40,38);
        if(GUI.Button(speaker,new GUIContent("",Help(m_Muted?"Unmute":"Mute")),m_Chip))SetViewerAudio(m_Volume,!m_Muted);
        Color ink=m_Muted?m_MutedLabel.normal.textColor:m_Label.normal.textColor;
        Fill(new Rect(speaker.x+9,speaker.y+15,5,9),ink);Fill(new Rect(speaker.x+14,speaker.y+11,4,17),ink);
        if(m_Muted)GUI.Label(new Rect(speaker.x+22,speaker.y+8,16,24),"×",m_Label);
        else {Fill(new Rect(speaker.x+23,speaker.y+14,2,11),ink);Fill(new Rect(speaker.x+29,speaker.y+10,2,19),ink);}
        float volume=GUI.HorizontalSlider(new Rect(right-140,y+14,80,20),m_Volume,0,1);
        GUI.Label(new Rect(right-52,y+6,44,28),$"{m_Volume*100:0}%",new GUIStyle(m_Mono){fontSize=12,alignment=TextAnchor.MiddleRight});
        if(volume!=m_Volume)SetViewerAudio(volume,m_Muted);
        GUI.enabled=enabled&&m_Timeline!=null;
        var area=new Rect(rect.x+126,y+66,rect.width-144,138);
        if(m_Timeline!=null)TimelineTracks(area,start,end,trial);
        string[] lanes={"Agent speech","Gaze samples","Events","Jev decisions"};
        for(int i=0;i<4;i++)GUI.Label(new Rect(rect.x+14,area.y+18+i*30,110,26),new GUIContent(lanes[i],i==3?"Green: speech/playback · orange: fade out · red: timeout/error · gray: pending/legacy · outlined: discarded":lanes[i]),m_Small);
        GUI.enabled=enabled;
        if(Action(new Rect(rect.x+12,rect.yMax-27,28,23),"?",m_ShortcutsOpen))m_ShortcutsOpen=!m_ShortcutsOpen;
        string notice=string.IsNullOrEmpty(m_ExportNotice)?m_Speed==1?"":"Audio plays at 1× only · Space: play / pause":m_ExportNotice;
        GUI.Label(new Rect(rect.x+48,rect.yMax-26,rect.width-64,20),notice,m_MutedLabel);
        if(m_ShortcutsOpen)
        {
            var help=new Rect(rect.x+12,rect.yMax-147,440,112);Fill(help,m_Border);Fill(new Rect(help.x+1,help.y+1,help.width-2,help.height-2),m_Raised);
            GUI.Label(new Rect(help.x+16,help.y+12,help.width-32,88),"Space: play / pause\nLeft / Right: previous / next sample\nDrag the playhead or any track · scene and data at cursor",m_Label);
        }
        GUI.enabled=enabled;
    }
    void TogglePlayback()
    {
        m_FollowLive=false;
        if(!m_Playing&&m_Time>=m_Timeline.Duration)Seek(0);
        m_Playing=!m_Playing;Sync(true);
    }
    void HandlePlaybackKeyboard(Event e,bool enabled,string focusedControl)
    {
        // Handle before buttons consume Space; text entry and modal dialogs keep their keys.
        if(e.type==EventType.KeyUp&&e.keyCode==KeyCode.Space){m_SpaceHeld=false;return;}
        if(!enabled||m_Timeline==null||m_TimelineDrag||focusedControl.StartsWith("text:",StringComparison.Ordinal)||
            e.type!=EventType.KeyDown||e.alt||e.control||e.command||e.shift)return;
        if(e.keyCode==KeyCode.Space)
        {
            if(!m_SpaceHeld)TogglePlayback();
            m_SpaceHeld=true;e.Use();
        }
        else if(e.keyCode==KeyCode.LeftArrow){Step(-1);e.Use();}
        else if(e.keyCode==KeyCode.RightArrow){Step(1);e.Use();}
    }
    void OnApplicationFocus(bool focused){if(!focused)m_SpaceHeld=false;}
    void TimelineTracks(Rect rect,double start,double end,TrialReplayRecord trial)
    {
        int id=GUIUtility.GetControlID(0x74726961,FocusType.Passive,rect);
        HandleTimelinePointer(rect,start,end,trial,id);
        double duration=Math.Max(.001,end-start);
        float X(double time)=>rect.x+(float)((time-start)/duration)*rect.width;
        // Roughly ten ticks, rounded to useful seconds at each range.
        double step=duration<=10?1:duration<=50?5:duration<=100?10:duration<=300?30:duration<=900?60:300;
        for(double t=0;t<=duration;t+=step)
        {float x=X(start+t);Fill(new Rect(x,rect.y+14,1,96),m_Border);if(!m_Timeline.Trials.Any(b=>Math.Abs(X(b.time)-x)<45))GUI.Label(new Rect(x+3,rect.y-4,65,20),$"{t:0}s",m_Small);}
        for(int lane=0;lane<4;lane++)Fill(new Rect(rect.x,rect.y+18+lane*30,rect.width,26),lane%2==0?m_TrackBackground:m_Raised);
        if(m_RunTimeline&&trial!=null)
        {
            var selected=m_Tracks.For(trial);
            float from=Mathf.Clamp(X(trial.time),rect.x,rect.xMax),to=Mathf.Clamp(X(selected?.End??trial.time),rect.x,rect.xMax);
            Fill(new Rect(from,rect.y+18,Mathf.Max(0,to-from),116),new Color(m_Info.r,m_Info.g,m_Info.b,.07f));
        }
        foreach(var boundary in m_Timeline.Trials)
        {
            if(boundary.time<start||boundary.time>end)continue;
            float bx=X(boundary.time);Fill(new Rect(bx,rect.y+14,1,122),m_Border);
            GUI.Label(new Rect(bx+4,rect.y-4,40,20),$"T{TrialNumber(boundary):00}",new GUIStyle(m_Small){font=m_MonoFont,normal={textColor=m_Label.normal.textColor}});
        }
        foreach(var segment in m_Audio.Plan.Segments)
        {
            if(segment.End<=start||segment.Start.time>end)continue;
            float left=Mathf.Max(rect.x,X(segment.Start.time)),right=Mathf.Min(rect.xMax,X(segment.End));
            if(right<=left)continue;
            var box=new Rect(left,rect.y+19,Mathf.Max(2,right-left),24);Fill(box,m_SpeechBackground);
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
        }
        var events=m_Tracks.Trials.Values.Where(item=>m_RunTimeline||item.Snapshot==trial).SelectMany(item=>item.Events)
            .Where(record=>record.time>=start&&record.time<=end&&!record.kind.StartsWith("audio_",StringComparison.Ordinal)&&!record.kind.StartsWith("readiness_",StringComparison.Ordinal)).OrderBy(record=>record.time).ThenBy(record=>record.sequence).ToList();
        for(int i=0;i<events.Count;i++)
        {
            var record=events[i];float x=Mathf.Min(rect.xMax-3,X(record.time));
            Fill(new Rect(x,rect.y+83,3,12),m_Info);
            float next=i+1<events.Count?X(events[i+1].time):rect.xMax;
            float gap=next-x;
            string tooltip=$"{Clock(record.time-start)} · {EventName(record.kind)} · {TrialReviewGuidanceState.Transition(record)}";
            if(GUI.Button(new Rect(x-3,rect.y+79,8,22),new GUIContent("",tooltip),GUIStyle.none)){m_Playing=false;Seek(record.time,record.sequence);}
            if(gap>=60)
            {
                float labelWidth=gap-15;
                string label=Ellipsis(EventName(record.kind),labelWidth,m_Small);
                if(GUI.Button(new Rect(x+9,rect.y+79,labelWidth,22),new GUIContent(label,tooltip),m_Small)){m_Playing=false;Seek(record.time,record.sequence);}
            }
        }
        JevTrack(rect,start,end,trial);
        float cursor=Mathf.Clamp(X(m_Time),rect.x,rect.xMax);
        Fill(new Rect(cursor,rect.y+12,2,129),m_Accent);
        Fill(new Rect(cursor-5,rect.y+5,12,10),m_Accent);
        var stamp=new Rect(Mathf.Clamp(cursor-64,rect.x,rect.xMax-128),rect.y-24,128,22);
        Fill(stamp,m_Surface);GUI.Label(stamp,"Study "+Clock(m_Time),m_Small);
    }
    static string Ellipsis(string text,float width,GUIStyle style)
    {
        if(style.CalcSize(new GUIContent(text)).x<=width)return text;
        while(text.Length>0&&style.CalcSize(new GUIContent(text+"…")).x>width)text=text.Substring(0,text.Length-1);
        return text.Length==0?"":text+"…";
    }
    void HandleTimelinePointer(Rect rect,double start,double end,TrialReplayRecord trial,int id)
    {
        var e=Event.current;
        if (GUI.enabled && e.type==EventType.MouseDown && e.button==0 && SelectJevAtPointer(rect,start,end,trial,e.mousePosition))
        { e.Use(); return; }
        // Capture before lane controls. The range stays fixed across trial boundaries;
        // pointer capture allows dragging beyond the tracks and clamps at the ends.
        if(GUI.enabled&&e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition))
        {m_TimelineDrag=true;m_DragStart=start;m_DragEnd=end;m_DragTrial=trial;m_DragControl=id;GUIUtility.hotControl=id;GUIUtility.keyboardControl=0;}
        if(m_TimelineDrag&&GUIUtility.hotControl==m_DragControl&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag||e.type==EventType.MouseUp))
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
