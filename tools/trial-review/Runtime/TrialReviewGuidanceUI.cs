using UnityEngine;

public sealed partial class TrialReviewApp
{
    readonly TrialReviewGuidanceState m_GuidanceState=new TrialReviewGuidanceState();
    bool m_ShowStateMarkers=true;
    static readonly Color GazeHitColor=new Color(1,.48f,.15f);
    static readonly Color VicinityColor=new Color(1,.79f,.016f);
    static readonly Color ControllerHitColor=new Color(.42f,.79f,1);

    void GuidanceInspector()
    {
        GUILayout.Space(12);GUILayout.Label("GUIDANCE STATE",m_Heading);GUILayout.Space(8);
        var state=m_GuidanceState.Current;
        if(state==null){GUILayout.Label(m_GuidanceState.Availability,m_MutedLabel);return;}
        Metric("Policy",string.IsNullOrEmpty(state.guidancePolicy)?"Not recorded":state.guidancePolicy);
        AssistanceInspector(state.assistance);
        Metric("Stage",state.phase);Metric("Allowed action",state.allowedAction);
        Metric("New guidance",state.version=="guidance-state-v2" ?
            (state.newGuidanceAllowed?"Allowed by state":"Blocked by state"):"Not recorded");
        Metric("Transition",$"{state.previousPhase} → {state.phase}");
        Metric("Reason",state.transitionReason);Metric("Revision",state.revision.ToString());
        Metric("Gaze hit",TrialReviewGuidanceState.Hit(state.gazeHitObjectId,state.gazeValid));
        Metric("Vicinity winner",TrialReviewGuidanceState.Hit(state.vicinityWinnerObjectId,state.gazeValid));
        Metric("Right ray hit",TrialReviewGuidanceState.Hit(state.controllerHitObjectId,state.controllerValid));
        GUILayout.Label("Orange: direct gaze · Gold: recorded vicinity winner · Blue: right controller. Intersections are not fixations.",m_MutedLabel);
    }

    void AssistanceInspector(AssistanceRuntimeSnapshot state)
    {
        if (state == null || string.IsNullOrEmpty(state.provider)) { Metric("Assistance source", "Not recorded"); return; }
        Metric("Selected", state.SelectionLabel);
        Metric("Model version", string.IsNullOrEmpty(state.modelVersion) ? "Not used" : state.modelVersion);
        Metric("Connection", state.ConnectionLabel);
        Metric("Last accepted", state.LastDecisionLabel);
        if (!string.IsNullOrEmpty(state.lastDecisionId))
        {
            Metric("Decision provider", state.lastDecisionProvider ?? "Not recorded");
            Metric("Decision ID", state.lastDecisionId);
        }
        GUILayout.Label("Selected is configuration. Queued means accepted for scheduling, not proof of playback; inspect the linked model decision for audio onset. Configured does not mean the cloud is reachable.", m_MutedLabel);
    }

    void DrawStateMarkers()
    {
        var state=m_GuidanceState.Current;
        if(!m_ShowStateMarkers||state==null||WaitingForHeadset||!m_Timeline.State.Visible||
            !m_Timeline.State.HasCurrentSample(m_Time))return;
        if(state.gazeValid)
        {
            StateMarker(state.gazeHitObjectId,"GAZE HIT",GazeHitColor,0);
            StateMarker(state.vicinityWinnerObjectId,"VICINITY",VicinityColor,1);
        }
        if(state.controllerValid)StateMarker(state.controllerHitObjectId,"RIGHT RAY",ControllerHitColor,2);
    }
    void StateMarker(string id,string label,Color color,int row)
    {
        if(string.IsNullOrEmpty(id)||!m_Renderer.Objects.TryGetValue(id,out var obj))return;
        Vector3 point=m_Renderer.Camera.WorldToViewportPoint(obj.transform.position);
        if(point.z<=m_Renderer.Camera.nearClipPlane||point.x<0||point.x>1||point.y<0||point.y>1)return;
        float width=102,height=19;
        float x=Mathf.Clamp(m_Viewport.x+point.x*m_Viewport.width+8,m_Viewport.x,m_Viewport.xMax-width);
        float y=Mathf.Clamp(m_Viewport.y+(1-point.y)*m_Viewport.height,m_Viewport.y,m_Viewport.yMax-60)+row*20;
        var box=new Rect(x,y,width,height);
        Fill(box,new Color(0,0,0,.86f));Fill(new Rect(x,y,3,height),color);
        GUI.Label(new Rect(x+7,y,width-7,height),new GUIContent(label,id),m_Small);
    }
}
