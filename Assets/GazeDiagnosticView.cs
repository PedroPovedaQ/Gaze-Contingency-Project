using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>Researcher eye check, separate from hidden experimental targets. Right-ray controls.</summary>
public sealed class GazeDiagnosticView : MonoBehaviour
{
    static readonly Vector2 k_PanelSize = new Vector2(880,800);
    const float k_CanvasScale = .0009f;
    public static GazeDiagnosticView Instance { get; private set; }
    public bool Visible { get; private set; }
    GameObject m_Panel, m_Launcher, m_Target;
    TextMeshProUGUI m_Text;
    SharedGazeStream m_Stream;
    FindObjectGameManager m_Game;
    int m_TargetIndex;
    string m_Set, m_ValidationId;
    Vector3 m_Anchor;
    Quaternion m_Heading;
    Pose m_PanelAnchor;
    double m_Start, m_LastRefresh;
    long m_Deliveries, m_Rows;
    void Start()
    {
        Instance=this; m_Stream=GetComponent<SharedGazeStream>();
        m_Stream.Observed+=Observe;
        m_Game=FindFirstObjectByType<FindObjectGameManager>();
        m_Panel=Canvas("Eye diagnostic panel",k_PanelSize);
        m_Text=Label(m_Panel.transform,new Vector2(0,120),new Vector2(830,490));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Quick eye alignment",new Vector2(-210,-185),new Vector2(395,60),()=>Begin("quick"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Clear alignment",new Vector2(210,-185),new Vector2(395,60),()=>
        { Finish(); m_Stream.ClearQuickAlignment("Cleared by researcher."); });
        RayMenuFeedback.CreateButton(m_Panel.transform,"Validation",new Vector2(-315,-270),new Vector2(195,60),()=>Begin("validation"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Offset fit",new Vector2(-105,-270),new Vector2(195,60),()=>Begin("fit"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Held-out",new Vector2(105,-270),new Vector2(195,60),()=>Begin("heldout"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Drift",new Vector2(315,-270),new Vector2(195,60),()=>Begin("drift"));
        RayMenuFeedback.CreateButton(m_Panel.transform,"Next target",new Vector2(-210,-350),new Vector2(195,60),Next);
        RayMenuFeedback.CreateButton(m_Panel.transform,"Finish check",new Vector2(0,-350),new Vector2(195,60),Finish);
        RayMenuFeedback.CreateButton(m_Panel.transform,"Close",new Vector2(210,-350),new Vector2(195,60),()=>SetVisible(false));
        m_Launcher=Canvas("Eye check launcher",new Vector2(230,70));
        RayMenuFeedback.CreateButton(m_Launcher.transform,"Eye check",Vector2.zero,new Vector2(220,60),()=>SetVisible(true));
        m_Panel.SetActive(false); m_Launcher.SetActive(false); m_Start=Time.realtimeSinceStartupAsDouble;
    }
    void Observe(GazeObservation sample) { m_Rows++; if(sample.newInputUpdate) m_Deliveries++; }
    public void SetVisible(bool visible)
    {
        // Never expose calibration stimuli during active experimental search.
        if(visible && (EntryGazeAlignment.IsShowing || (m_Game!=null && m_Game.SearchActive))) return;
        if(visible && !Visible && Camera.main!=null)
        {
            m_PanelAnchor=MenuPose(Camera.main.transform);
            Position(m_Panel,m_PanelAnchor,PanelOffset(m_Target!=null));
        }
        Visible=visible; if(m_Panel!=null)m_Panel.SetActive(visible);
        if(!visible && m_Set!=null) Finish();
    }
    void Update()
    {
        if(m_Panel==null || Camera.main==null) return;
        bool search=m_Game!=null && m_Game.SearchActive;
        // Researcher launch control is only available in setup or an explicit pause, not trial readiness.
        bool canCheck=!EntryGazeAlignment.IsShowing && (m_Game==null || m_Game.ResearcherPaused || m_Game.CurrentState!=FindObjectGameManager.GameState.Playing);
        if(search || !canCheck) SetVisible(false);
        bool showLauncher=!Visible && canCheck && !search;
        if(showLauncher && !m_Launcher.activeSelf)
            Position(m_Launcher,MenuPose(Camera.main.transform),new Vector3(.55f,-.4f,1.1f));
        m_Launcher.SetActive(showLauncher);
        if (m_Set == "quick")
        {
            if (!m_Stream.QuickAlignment.IsRunning) Finish();
            else if (m_TargetIndex != m_Stream.QuickAlignment.TargetIndex)
            { m_TargetIndex = m_Stream.QuickAlignment.TargetIndex; PlaceTarget(); }
        }
        if(!Visible || Time.realtimeSinceStartupAsDouble-m_LastRefresh<.1) return;
        m_LastRefresh=Time.realtimeSinceStartupAsDouble;
        if (m_Set == "quick")
        {
            m_Text.text = "<b>Quick eye alignment</b>\n\n" +
                $"Dot {m_TargetIndex + 1} of 3\n\n{m_Stream.QuickAlignment.Status}\n\n" +
                "Look at each dot with your eyes and keep your head still.\n" +
                "The dots will change automatically.\n\n" +
                "Complete HTC eye calibration before this check.\n" +
                "Finish check or Close will cancel an unfinished alignment.";
            return;
        }
        var s=m_Stream.Latest;if(s==null)return;
        double elapsed=Math.Max(.001,Time.realtimeSinceStartupAsDouble-m_Start);
        string error=s.valid && s.validationTargetPresent ? s.targetErrorDegrees.ToString("F2")+" deg" : "not available";
        m_Text.text=$"<b>Eye measurement — researcher check</b>\n"+
            $"Valid: {s.valid}  {s.invalidReason}  Ray: {s.raySource}\nDelivery age: {s.ageSeconds*1000:F1} ms (sensor age unknown)\n"+
            $"Input updates: {m_Deliveries/elapsed:F1}/s; rows: {m_Rows/elapsed:F1}/s\n<b>Sensor 120 Hz: unverified</b>\n"+
            $"{NativeGazeRecorder.Instance?.Status ?? "Native timestamps unavailable."}\n"+
            $"Raw origin: {s.rawWorldOrigin:F3}\nRaw direction: {s.rawWorldDirection:F3}\n"+
            $"Head: {s.headPosition:F2}; rotation: {s.headRotation.eulerAngles:F1}\n"+
            $"HTC L/R valid: {s.htcGazeAvailable} / {s.leftValid}/{s.rightValid}\n"+
            $"Correction: {s.correction}; EXT error: {error}; used: {(s.valid && s.validationTargetPresent ? s.usedTargetErrorDegrees.ToString("F2")+" deg" : "n/a")}\n"+
            $"X-mirrored-origin comparison: {(s.valid && s.validationTargetPresent ? s.mirroredOriginErrorDegrees.ToString("F2") : "n/a")} deg\n"+
            $"<b>Quick alignment:</b> {m_Stream.QuickAlignment.Status}\n"+
            $"{m_Set ?? "Choose a separate check below."}  target {m_TargetIndex+1}\n"+
            (m_Target!=null ? "Turn toward the dot; hold gaze, then Next.\n" : "")+
            (m_Stream.RecordingError!=null ? "<color=red>Recording error: "+m_Stream.RecordingError+"</color>" : "Logging raw samples, invalid states and timing.");
    }
    public void Begin(string set)
    {
        if (EntryGazeAlignment.IsShowing) return;
        if(m_Game!=null && m_Game.CurrentState==FindObjectGameManager.GameState.Playing && !m_Game.ResearcherPaused)return;
        if(Camera.main==null)return;
        Finish();m_Set=set;m_ValidationId=Guid.NewGuid().ToString("N");m_TargetIndex=0;
        m_Anchor=Camera.main.transform.position;m_Heading=Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0);
        if (set == "quick")
        {
            if (m_Stream.RecordingError != null || string.IsNullOrEmpty(m_Stream.RecordingPath))
            { m_Set = null; return; }
            m_Stream.QuickAlignment.Begin(m_Anchor, Camera.main.transform.rotation, Time.realtimeSinceStartupAsDouble);
            m_ValidationId = m_Stream.QuickAlignment.Report.id;
        }
        m_PanelAnchor=new Pose(m_Anchor,m_Heading);
        Position(m_Panel,m_PanelAnchor,PanelOffset(true));
        m_Target=GameObject.CreatePrimitive(PrimitiveType.Sphere);m_Target.name="Independent eye validation dot";
        Destroy(m_Target.GetComponent<Collider>());m_Target.transform.localScale=Vector3.one*.025f;
        m_Target.GetComponent<Renderer>().material.color=Color.magenta;
        PlaceTarget();
    }
    public void Next() { if(m_Target==null || m_Set=="quick")return; m_TargetIndex++;PlaceTarget(); }
    void PlaceTarget()
    {
        if (m_Set == "quick")
        {
            m_Target.transform.position = m_Stream.QuickAlignment.CurrentTarget;
            m_Stream.SetValidationTarget(m_Target.transform,m_ValidationId,m_Set,"target_"+m_TargetIndex);
            return;
        }
        int count=m_Set=="drift"?5:m_Set=="fit"?9:54;
        if(m_TargetIndex>=count){Finish();return;}
        // The fit grid and held-out targets are disjoint. All positions are world-fixed while sampled.
        float yaw=(m_TargetIndex%3-1)*15, pitch=(m_TargetIndex/3%3-1)*15, distance=2;
        if(m_Set=="drift") { yaw=new[]{0f,-15,15,0,0}[m_TargetIndex];pitch=new[]{0f,0,0,-15,15}[m_TargetIndex]; }
        else if(m_Set!="fit")
        {
            float[] sectors={0,45,90,180,270,22.5f};
            yaw=sectors[m_TargetIndex/9];pitch=(m_TargetIndex%3-1)*12;
            distance=new[]{1.2f,2.4f,3.2f}[m_TargetIndex/3%3];
            if(m_Set=="heldout") { yaw+=7;pitch+=3;distance+=.15f; }
        }
        m_Target.transform.position=m_Anchor+m_Heading*Quaternion.Euler(-pitch,yaw,0)*Vector3.forward*distance;
        m_Stream.SetValidationTarget(m_Target.transform,m_ValidationId,m_Set,"target_"+m_TargetIndex);
    }
    public void Finish()
    {
        if (m_Set == "quick" && m_Stream != null && m_Stream.QuickAlignment.IsRunning)
            m_Stream.ClearQuickAlignment("Quick alignment cancelled before completing all checks.");
        bool hadTarget=m_Target!=null;
        if(m_Target!=null)Destroy(m_Target);m_Target=null;
        if (m_Stream != null && m_ValidationId != null && m_Stream.ValidationId == m_ValidationId)
            m_Stream.SetValidationTarget(null,null,null,null);
        m_Set=null;
        if(hadTarget && m_Panel!=null)Position(m_Panel,m_PanelAnchor,PanelOffset(false));
    }
    static Pose MenuPose(Transform head) => new Pose(head.position,Quaternion.Euler(0,head.eulerAngles.y,0));
    static void Position(GameObject panel,Pose anchor,Vector3 offset)
    {if(panel!=null)panel.transform.SetPositionAndRotation(anchor.position+anchor.rotation*offset,anchor.rotation);}
    // Keep the opaque panel below the local eye-validation grid. At its idle
    // height it covers the +15-degree fit dots at middle and low elevation.
    static Vector3 PanelOffset(bool targetActive) => new Vector3(.75f,targetActive?-.85f:-.15f,1.35f);
    static GameObject Canvas(string name,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(TrackedDeviceGraphicRaycaster));
        var background=go.GetComponent<Image>();background.color=new Color(.025f,.045f,.065f,.94f);background.raycastTarget=false;
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=Camera.main;
        go.GetComponent<RectTransform>().sizeDelta=size;go.transform.localScale=Vector3.one*k_CanvasScale;
        return go;
    }
    static TextMeshProUGUI Label(Transform parent,Vector2 position,Vector2 size)
    {
        var go=new GameObject("Diagnostic values",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var text=go.GetComponent<TextMeshProUGUI>();text.rectTransform.sizeDelta=size;text.rectTransform.anchoredPosition=position;
        text.fontSize=22;text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=22;
        text.color=Color.white;text.raycastTarget=false;
        return text;
    }
    void OnDestroy()
    {
        if(m_Stream!=null)m_Stream.Observed-=Observe;Finish();
        if(m_Panel!=null)Destroy(m_Panel);if(m_Launcher!=null)Destroy(m_Launcher);if(Instance==this)Instance=null;
    }
}
