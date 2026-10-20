using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Researcher overlay: live vicinity policy applied to reconstructed colliders.
/// Source hits and guidance decisions are never rewritten by this visualization.</summary>
public sealed class TrialReviewVicinity : IDisposable
{
    readonly List<GameObject> m_Objects=new List<GameObject>();
    readonly List<TrialReplayRecord> m_PaddingRecords=new List<TrialReplayRecord>();
    TrialReplayTimeline m_Timeline;
    TrialReplayRenderer m_Renderer;
    string m_Trial;
    int m_Indexed;
    LineRenderer m_Ring;
    Material m_RingMaterial;
    public string WinnerId { get; private set; }
    public bool RingVisible => m_Ring!=null&&m_Ring.enabled;
    public float Padding { get; private set; }=6;
    public bool RecordedPadding { get; private set; }
    public bool RecordedWinner { get; private set; }

    public void Prepare(TrialReplayTimeline timeline,TrialReplayRenderer renderer,double time,long sequence,
        bool gazeAvailable,Ray gaze,bool showRing,GuidanceStateSnapshot captured=null)
    {
        if(m_Timeline!=timeline){m_Timeline=timeline;m_Indexed=0;m_PaddingRecords.Clear();}
        while(m_Indexed<timeline.Records.Count)
        {
            var record=timeline.Records[m_Indexed++];
            if(record.guidanceDecision?.targetVicinityMeasured==true)m_PaddingRecords.Add(record);
        }
        var state=timeline.State;
        if(m_Renderer!=renderer||m_Trial!=state.Trial?.trialId)
        {
            m_Renderer=renderer;m_Trial=state.Trial?.trialId;m_Objects.Clear();
            if(state.Trial!=null)foreach(var saved in state.Trial.objects)
            {
                var obj=renderer.Objects[saved.id];
                int shape=Array.IndexOf(new[]{"Sphere","Cube","Pyramid","Cylinder","Star","Capsule"},saved.shape);
                if(shape>=0)
                {
                    TrialReviewVicinityPolicy.ConfigureCollider(obj,null,shape);
                    m_Objects.Add(obj);
                }
                obj.GetComponent<Renderer>().sharedMaterial.shader=Resources.Load<Shader>("TrialReviewObject");
            }
        }
        Padding=6;RecordedPadding=false;
        for(int i=m_PaddingRecords.Count-1;i>=0;i--)
        {
            var r=m_PaddingRecords[i];
            if(r.time>time||r.time==time&&r.sequence>sequence||r.trialId!=m_Trial)continue;
            float padding=r.guidanceDecision.targetVicinityPaddingDegrees;
            if(float.IsFinite(padding)){Padding=Mathf.Clamp(padding,0,20);RecordedPadding=true;}
            break;
        }
        Physics.SyncTransforms(); // Collider bounds must follow this cursor's saved transforms, even when scrubbing backward.
        bool valid=state.Visible&&state.Searching&&renderer.SampleCurrent&&gazeAvailable;
        GameObject winner=valid?TrialReviewVicinityPolicy.ClosestObject(gaze.origin,gaze.direction,m_Objects,Padding,out _,out _,out _):null;
        if(!TrialReviewGuidanceState.IsRecorded(captured))captured=null;
        RecordedWinner=captured!=null;
        if(captured!=null)
        {
            winner=null;
            valid=valid&&captured.gazeValid;
            if(valid&&!string.IsNullOrEmpty(captured.vicinityWinnerObjectId))
                renderer.Objects.TryGetValue(captured.vicinityWinnerObjectId,out winner);
        }
        WinnerId=winner!=null?winner.name:null;
        if(m_Ring!=null)m_Ring.enabled=false;
        var sample=renderer.SampleCurrent?state.Sample:null;
        if(state.Trial==null)return;
        foreach(var saved in state.Trial.objects)
        {
            var obj=renderer.Objects[saved.id];
            Color color=renderer.ShowTarget&&saved.target?Color.green:saved.rgba;
            Color rim=Color.clear;
            if(valid&&winner==obj){color*=1.6f;color.a=saved.rgba.a;rim=new Color(1,.79f,.016f);}
            if(state.Visible&&renderer.SampleCurrent&&renderer.ShowControllers&&(captured!=null
                ?captured.controllerValid&&captured.controllerHitObjectId==saved.id
                :TrialReplayRenderer.ControllerUsable(sample?.rightController)&&sample.rightController.hoveredId==saved.id))
                rim=new Color(.7f,.87f,1); // Controller rim retains priority, as in the study.
            if(state.Selection!=null&&state.Selection.objectId==saved.id&&time-state.Selection.time<=.3)color=Color.white;
            var material=obj.GetComponent<Renderer>().sharedMaterial;
            material.color=color;material.SetColor("_RimColor",rim);
            if(showRing&&valid&&saved.target&&obj.TryGetComponent<Collider>(out var collider)&&
                TrialReviewVicinityPolicy.Measure(gaze.origin,gaze.direction,collider.bounds,Padding,out _,out float radius))
                Draw(gaze.origin,collider.bounds,radius,winner==obj);
        }
    }
    void Draw(Vector3 origin,Bounds bounds,float degrees,bool inside)
    {
        if(m_Ring==null)
        {
            m_Ring=new GameObject("Reconstructed target detection ring").AddComponent<LineRenderer>();
            m_RingMaterial=new Material(Shader.Find("Unlit/Color"));m_Ring.sharedMaterial=m_RingMaterial;
            m_Ring.useWorldSpace=true;m_Ring.loop=true;m_Ring.positionCount=64;m_Ring.widthMultiplier=.008f;
        }
        m_Ring.enabled=true;m_RingMaterial.color=inside?Color.green:Color.cyan;
        var delta=bounds.center-origin;var facing=Quaternion.LookRotation(delta);
        float radius=delta.magnitude*Mathf.Tan(degrees*Mathf.Deg2Rad);
        for(int i=0;i<64;i++)
        {
            float angle=i*2*Mathf.PI/64;
            m_Ring.SetPosition(i,bounds.center+facing*new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*radius);
        }
    }
    public void Dispose()
    {
        if(m_Ring!=null)UnityEngine.Object.DestroyImmediate(m_Ring.gameObject);
        if(m_RingMaterial!=null)UnityEngine.Object.DestroyImmediate(m_RingMaterial);
    }
}
