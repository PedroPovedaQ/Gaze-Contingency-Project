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
        m_HeadingColor=m_LightTheme?new Color(.38f,.28f,.02f):m_Accent;
        m_TrackBackground=m_LightTheme?new Color(.89f,.88f,.84f):new Color(.065f,.065f,.06f);
        m_SpeechBackground=m_LightTheme?new Color(.90f,.79f,.44f):new Color(.34f,.28f,.10f);
        Color text=m_LightTheme?new Color(.09f,.09f,.08f):new Color(.95f,.95f,.93f);
        Color muted=m_LightTheme?new Color(.35f,.34f,.31f):new Color(.70f,.69f,.64f);
        m_Panel=Solid(m_Surface);m_ButtonTexture=Solid(m_LightTheme?new Color(.89f,.88f,.84f):new Color(.14f,.14f,.125f));
        m_SelectedTexture=Solid(m_Accent);m_HoverTexture=Solid(m_LightTheme?new Color(.93f,.86f,.65f):new Color(.25f,.22f,.13f));
        m_Label=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true,richText=false,normal={textColor=text}};
        m_MutedLabel=new GUIStyle(m_Label){fontSize=12,normal={textColor=muted}};
        m_Subtitle=new GUIStyle(m_Label){fontSize=22,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
        m_Title=new GUIStyle(m_Label){fontSize=23,fontStyle=FontStyle.Bold};
        m_Heading=new GUIStyle(m_Label){fontSize=13,fontStyle=FontStyle.Bold,normal={textColor=m_HeadingColor}};
        m_Center=new GUIStyle(m_Label){alignment=TextAnchor.MiddleCenter,wordWrap=false};
        m_Small=new GUIStyle(m_MutedLabel){alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip};
        m_Button=new GUIStyle(GUI.skin.button){fontSize=14,richText=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(12,12,9,9),border=new RectOffset(0,0,0,0)};
        m_Button.normal.background=m_ButtonTexture;m_Button.normal.textColor=m_Label.normal.textColor;
        m_Button.hover.background=m_HoverTexture;m_Button.hover.textColor=text;
        m_Button.active.background=m_SelectedTexture;m_Button.active.textColor=Color.black;
        m_Button.focused.background=m_HoverTexture;m_Button.focused.textColor=text;
        m_Chip=new GUIStyle(m_Button){fontSize=13,padding=new RectOffset(8,8,7,7)};
        m_ActiveChip=new GUIStyle(m_Chip);m_ActiveChip.normal.background=m_SelectedTexture;m_ActiveChip.normal.textColor=Color.black;
        m_ActiveChip.hover.background=m_SelectedTexture;m_ActiveChip.hover.textColor=Color.black;
        m_ActiveChip.focused.background=m_SelectedTexture;m_ActiveChip.focused.textColor=Color.black;
        // Independent view options use radio-like indicators, not gold selection blocks.
        m_ViewToggleOff=new GUIStyle(m_Chip){richText=true,padding=new RectOffset(8,8,2,2)};
        foreach(var state in new[]{m_ViewToggleOff.normal,m_ViewToggleOff.hover,m_ViewToggleOff.active,m_ViewToggleOff.focused})
        {state.background=m_ButtonTexture;state.textColor=text;}
        m_ViewToggleOn=new GUIStyle(m_ViewToggleOff){fontStyle=FontStyle.Bold};
        m_Row=new GUIStyle(m_Button){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=14,padding=new RectOffset(12,10,9,9)};
        m_ActiveRow=new GUIStyle(m_Row);m_ActiveRow.normal.background=m_HoverTexture;m_ActiveRow.normal.textColor=m_HeadingColor;
        m_Field=new GUIStyle(GUI.skin.textField){fontSize=14,padding=new RectOffset(9,9,9,9),normal={textColor=text,background=m_ButtonTexture}};
        m_Field.focused.background=m_HoverTexture;m_Field.focused.textColor=text;
        m_Field.hover.background=m_ButtonTexture;m_Field.hover.textColor=text;
        m_Field.active.background=m_HoverTexture;m_Field.active.textColor=text;
        m_Code=new GUIStyle(m_MutedLabel){fontSize=11,wordWrap=true};
        m_WaitingTitle=new GUIStyle(m_Heading){alignment=TextAnchor.MiddleCenter};
        m_DegreeLabel=new GUIStyle(m_Heading){fontSize=13,alignment=TextAnchor.MiddleCenter,wordWrap=false};
        m_DegreeLabel.normal.textColor=m_Accent; // Scene labels always sit on a dark backing.
        GUI.skin.settings.cursorColor=text;GUI.skin.settings.selectionColor=new Color(.70f,.55f,.10f,.65f);
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
        Styles();
        // Keep controls comfortably sized on both retina and small desktop windows.
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float width=Screen.width/scale,height=Screen.height/scale;
        Fill(new Rect(0,0,width,height),m_Background);
        GUI.enabled=!m_Browser&&!m_Loading&&!m_DatasetPanel;
        HandlePlaybackKeyboard(Event.current, GUI.enabled, GUI.GetNameOfFocusedControl());
        Header(width);
        if(m_LivePanel)LiveControls(width);
        float top=m_LivePanel?(m_FollowLive?210:164):78,bottom=height-260,rail=204,inspector=m_Tab==3?380:274;
        Panel(new Rect(0,top,rail,bottom-top));Panel(new Rect(width-inspector,top,inspector,bottom-top));
        float left=rail+14,viewWidth=width-inspector-14-left;
        m_OptionColumns=Mathf.Clamp(Mathf.FloorToInt(viewWidth/145),3,8);
        float optionsHeight=Mathf.Ceil(11f/m_OptionColumns)*42+26;
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
        GUI.enabled=true;
        if(m_Loading){Panel(new Rect(width/2-190,height/2-40,380,80));GUI.Label(new Rect(width/2-180,height/2-30,360,60),"Loading recording…",m_Center);}
        if(m_Browser)Browser(new Rect(width/2-380,80,760,height-160));
        if(m_DatasetPanel)DatasetDialog(width,height);
        DrawHoverHelp(width,height);
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
    void Header(float width)
    {
        Panel(new Rect(0,0,width,64));Fill(new Rect(0,62,width,2),m_Accent);
        GUI.Label(new Rect(20,17,180,32),"Trial Review",m_Title);
        string participant=AssignedParticipantId;
        if(string.IsNullOrEmpty(participant))participant=m_Timeline?.State.Trial?.participantCode;
        if(!string.IsNullOrEmpty(m_ParticipantName))participant=m_ParticipantName;
        int run=m_Timeline?.State.Trial?.runNumber??m_Timeline?.Header.runNumber??0;
        string name=m_Timeline==null?"Choose a session":$"{(string.IsNullOrEmpty(participant)?"Recorded session":participant)} / {(run>0?$"Run {run}":"Session")}";
        if(!string.IsNullOrEmpty(DatasetName))name=DatasetName;
        if(Action(new Rect(222,14,192,36),name))OpenDatasetPanel(2);
        if(Action(new Rect(420,14,94,36),"Dataset"))OpenDatasetPanel();
        string status=m_FollowLive?(WaitingForHeadset?"Waiting for signal":m_DrainingLive?"Buffered replay":"Following live"):m_ViewLive?"Reviewing archive":"Saved replay";
        GUI.Label(new Rect(526,20,165,24),status,m_Heading);
        if(Action(new Rect(width-666,14,118,36),m_LightTheme?"Dark mode":"Light mode"))ToggleTheme();
        if(Action(new Rect(width-540,14,124,36),"Open run…"))Browse(false);
        if(Action(new Rect(width-408,14,76,36),"Demo"))StartCoroutine(Open(Path.Combine(Application.streamingAssetsPath,"Demo/recording.jsonl")));
        if(m_DetachedReview)GUI.Label(new Rect(width-324,20,310,30),"Detached review · live stays in main window",m_MutedLabel);
        else
        {
            if(Action(new Rect(width-324,14,144,36),"Wi-Fi settings",m_LivePanel))m_LivePanel=!m_LivePanel;
            LiveModeSwitch(new Rect(width-172,14,152,36));
        }
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
        GUI.SetNextControlName("text:trial-search");
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
            if(GUILayout.Button($"{(selected?"› ":"")}{TrialNumber(trial):00}     {duration}\n|θ| {trial.absoluteTheta}°  ·  {(track?.Completed==true?"Selected":track?.TimedOut==true?"Time up":"Recorded")}",selected?m_ActiveRow:m_Row,GUILayout.Height(59)))SelectTrial(trial);
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
        if(m_Renderer.RecordedHead&&!m_Renderer.UsingRecordedHead)GUI.Label(new Rect(m_Viewport.x+12,m_Viewport.y+12,m_Viewport.width-24,30),"Head pose unavailable · overview shown",m_Heading);
        m_ShowEyeRay=ViewToggle(0,m_ShowEyeRay,recordedGaze?"Eye gaze":m_Correction.Supported?"Corrected gaze":"Eye ray (display)");
        m_Renderer.ShowControllers=ViewToggle(1,m_Renderer.ShowControllers,"Controllers");
        m_ShowBody=ViewToggle(2,m_ShowBody,"Human model");
        m_ShowWalls=ViewToggle(3,m_ShowWalls,"Viewer walls");
        m_ShowDegrees=ViewToggle(4,m_ShowDegrees,"Degrees");
        m_ShowRoom=ViewToggle(5,m_ShowRoom,"Room");
        m_Renderer.ShowTarget=ViewToggle(6,m_Renderer.ShowTarget,"Reveal target");
        m_Renderer.ShowGaze=ViewToggle(7,m_Renderer.ShowGaze,recordedGaze?"Before alignment":"Raw gaze");
        m_ShowDetectionRing=ViewToggle(8,m_ShowDetectionRing,"Detection ring");
        m_ShowSubtitles=ViewToggle(9,m_ShowSubtitles,"Subtitles");
        m_ShowStateMarkers=ViewToggle(10,m_ShowStateMarkers,"State markers");
        GUI.Label(new Rect(m_Viewport.x,m_Viewport.yMax+8+Mathf.Ceil(11f/m_OptionColumns)*42,m_Viewport.width,20),
            $"{(m_Vicinity.RecordedWinner?"Recorded highlight":"Reconstructed highlight (not recorded)")} · ring estimated · padding {m_Vicinity.Padding:0.#}° ({(m_Vicinity.RecordedPadding?"recorded":"default")})",m_MutedLabel);
        var e=Event.current;
        if(GUI.enabled&&m_Viewport.Contains(e.mousePosition)&&!m_Renderer.RecordedHead&&!WaitingForHeadset)
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
        var box=new Rect(m_Viewport.center.x-width/2,m_Viewport.yMax-height-14,width,height);
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
    bool ViewToggle(int index,bool value,string label)
    {
        float width=(m_Viewport.width-(m_OptionColumns-1)*6)/m_OptionColumns;
        var rect=new Rect(m_Viewport.x+(index%m_OptionColumns)*(width+6),m_Viewport.yMax+7+(index/m_OptionColumns)*42,width,35);
        // Double the indicator size without enlarging the label or changing the hit area.
        if(GUI.Button(rect,new GUIContent("<size=26>"+(value?"◉":"○")+"</size>  "+label,Help(label)),value?m_ViewToggleOn:m_ViewToggleOff))value=!value;
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
        string[] tabs={"At cursor","Events","Final data","Jev","Evaluation"};
        for(int i=0;i<tabs.Length;i++)
        {
            if(i==3){GUILayout.EndHorizontal();GUILayout.BeginHorizontal();}
            if(GUILayout.Button(HelpContent(tabs[i]),m_Tab==i?m_ActiveChip:m_Chip)){m_Tab=i;m_InspectScroll=Vector2.zero;}
        }
        GUILayout.EndHorizontal();GUILayout.Space(18);
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
            RunExportControls();
            GUILayout.Label("FINAL TRIAL RESULTS",m_Heading);
            GUILayout.Label("Final summary context, not values at the cursor. Block surveys repeat across trials.",m_MutedLabel);
            if(Button("Choose CSV export…"))Browse(true);
            var row=m_Summary?.For(trial);
            if(row==null)GUILayout.Label("No matching summary loaded. Choose a completed export with manifest.json and trials.csv.",m_Label);
            else foreach(var pair in row){GUILayout.Label(pair.Key,m_Heading);GUILayout.Label(string.IsNullOrEmpty(pair.Value)?"Unavailable":pair.Value,m_Label);GUILayout.Space(8);}
            GUILayout.EndScrollView();
        }
        else
        {
            m_InspectScroll=GUILayout.BeginScrollView(m_InspectScroll);
            GUILayout.Label(trial==null?"No trial":$"{(trial.practice?"Practice":"Trial")} {TrialNumber(trial):00}",m_Title);GUILayout.Space(14);
            Metric("Voice",Voice(trial?.voice));Metric("Study time",Clock(m_Time));Metric("Trial time",Clock(m_Time-(trial?.time??0)));Metric("Search",state.Searching?"Active":"Inactive");
            GuidanceInspector();
            ThetaInspector();
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
            if(m_RawExpanded&&sample!=null){GUILayout.Label($"Source line {m_Timeline.SampleSourceLine} · historical values remain during gaps",m_MutedLabel);GUI.SetNextControlName("text:raw-data");GUILayout.TextArea(JsonUtility.ToJson(sample,true),m_Code);}
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
        if(Action(new Rect(x,y,68,38),m_Playing?"Pause":"Play",m_Playing))TogglePlayback();
        if(Action(new Rect(x+76,y,70,38),"‹ Event"))JumpEvent(-1);
        if(Action(new Rect(x+152,y,70,38),"Event ›"))JumpEvent(1);
        double start=0,end=m_Timeline?.Duration??1;
        var trial=m_Timeline?.State.Trial;var track=m_Tracks.For(trial);
        if(!m_RunTimeline&&track!=null){start=trial.time;end=Math.Max(start+.001,track.End);}
        if(m_TimelineDrag){start=m_DragStart;end=m_DragEnd;trial=m_DragTrial;}
        GUI.Label(new Rect(x+236,y+7,230,25),$"Study {Clock(m_Time)}  ·  Trial {Clock(m_Time-(m_Timeline?.State.Trial?.time??0))}",m_Label);
        if(Action(new Rect(x+457,y,74,38),$"{m_Speed:0.##}×"))
        {float[] speeds={.25f,.5f,1,2};m_Speed=speeds[(Array.IndexOf(speeds,m_Speed)+1)%speeds.Length];m_FollowLive=false;Sync(true);}
        GUI.enabled=enabled; // Audio controls also work before the first live trial.
        if(Action(new Rect(x+541,y,70,38),m_Muted?"Unmute":"Mute",m_Muted))SetViewerAudio(m_Volume,!m_Muted);
        float volume=GUI.HorizontalSlider(new Rect(x+623,y+6,95,20),m_Volume,0,1);
        GUI.Label(new Rect(x+623,y+23,100,18),$"Voice {m_Volume*100:0}%",m_Small);
        if(volume!=m_Volume)SetViewerAudio(volume,m_Muted);
        GUI.enabled=enabled&&m_Timeline!=null;
        if(Action(new Rect(x+734,y,85,38),"‹ Sample"))Step(-1);
        if(Action(new Rect(x+825,y,85,38),"Sample ›"))Step(1);
        if(Action(new Rect(rect.xMax-350,y,130,38),m_RunTimeline?"Whole session":"Selected trial",m_RunTimeline))m_RunTimeline=!m_RunTimeline;
        GUI.enabled=enabled&&trial!=null&&!m_ViewLive;
        if(Action(new Rect(rect.xMax-210,y,198,38),"Export trial data…"))ExportTrial();
        GUI.enabled=enabled&&m_Timeline!=null;
        var area=new Rect(rect.x+126,y+59,rect.width-144,138);
        if(m_Timeline!=null)TimelineTracks(area,start,end,trial);
        for(int i=0;i<4;i++)GUI.Label(new Rect(rect.x+14,area.y+18+i*30,110,24),new[]{"Agent speech","Gaze samples","Events","Jev decisions"}[i],m_MutedLabel);
        string notice=string.IsNullOrEmpty(m_ExportNotice)?m_Speed==1?"Drag the playhead or any track · scene and data at cursor · Left / Right: sample · Space: play / pause":"Audio plays at 1× only · Space: play / pause":m_ExportNotice;
        GUI.Label(new Rect(rect.x+14,rect.yMax-24,rect.width-28,20),notice,m_MutedLabel);
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
        {float x=X(start+t);Fill(new Rect(x,rect.y+14,1,96),m_Border);GUI.Label(new Rect(x+3,rect.y-4,65,20),$"{t:0}s",m_Small);}
        for(int lane=0;lane<4;lane++)Fill(new Rect(rect.x,rect.y+18+lane*30,rect.width,26),m_TrackBackground);
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
            float lastLabel=-1000;
            foreach(var record in item.Events)
            {
                if(record.time<start||record.time>end||record.kind.StartsWith("audio_",StringComparison.Ordinal)||record.kind.StartsWith("readiness_",StringComparison.Ordinal))continue;
                float x=X(record.time);Fill(new Rect(x,rect.y+83,3,12),m_Accent);
                string label=x-lastLabel>125?EventName(record.kind):"";
                if(label.Length>0)lastLabel=x;
                if(GUI.Button(new Rect(x-3,rect.y+79,Mathf.Min(label.Length>0?122:8,rect.xMax-x+3),22),new GUIContent("  "+label,$"{Clock(record.time-start)} · {EventName(record.kind)} · {TrialReviewGuidanceState.Transition(record)}"),m_Small))
                {m_Playing=false;Seek(record.time,record.sequence);}
            }
        }
        JevTrack(rect,start,end,trial);
        float cursor=Mathf.Clamp(X(m_Time),rect.x,rect.xMax);
        Fill(new Rect(cursor,rect.y+12,2,129),m_Accent);
        Fill(new Rect(cursor-5,rect.y+5,12,10),m_Accent);
        var stamp=new Rect(Mathf.Clamp(cursor-64,rect.x,rect.xMax-128),rect.y-24,128,22);
        Fill(stamp,m_Surface);GUI.Label(stamp,"Study "+Clock(m_Time),m_Small);
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
