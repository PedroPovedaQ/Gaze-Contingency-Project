using System;
using System.Linq;
using UnityEngine;

/// <summary>Recorded head/controllers with illustrative seated body, arm IK and an optional eye-origin ray.</summary>
public sealed class TrialReviewActors : IDisposable
{
    readonly GameObject m_Body,m_Left,m_Right;
    readonly TrialReviewArm m_LeftArm,m_RightArm;
    readonly Transform m_LeftGrip,m_RightGrip;
    readonly LineRenderer m_EyeRay;
    readonly Material m_EyeMaterial;
    public TrialReviewArm LeftArm => m_LeftArm;
    public TrialReviewArm RightArm => m_RightArm;
    public Transform LeftGrip => m_LeftGrip;
    public Transform RightGrip => m_RightGrip;
    public LineRenderer EyeRay => m_EyeRay;
    public bool GazeAvailable { get; private set; }
    public Ray GazeRay { get; private set; }
    public float GazeOriginOffset { get; private set; }
    public float CorrectedHeadOffset { get; private set; }
    public bool CorrectionApplied { get; private set; }
    public bool ArmReachLimited => m_LeftArm.ReachLimited || m_RightArm.ReachLimited;
    TrialReplayTimeline m_Timeline;
    TrialReviewTorsoTrack m_Torso;
    public float TorsoYaw => m_Body.transform.eulerAngles.y;
    readonly Transform m_Head,m_Anchor,m_ChairBase;
    readonly Quaternion m_HeadBindRotation,m_AnchorBindRotation;
    public bool BodyVisible => m_Body.activeSelf;
    public bool LeftVisible => m_Left.activeSelf;
    public bool RightVisible => m_Right.activeSelf;
    public Transform HeadAnchor => m_Anchor;
    public Transform RightController => m_Right.transform;
    public TrialReviewActors()
    {
        m_Body=Spawn("ReplayParticipant");m_Left=Spawn("ReplayControllerL");m_Right=Spawn("ReplayControllerR");
        m_Head=m_Body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
        m_Anchor=m_Body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="RecordedHeadAnchor");
        m_ChairBase=m_Body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="ChairBase");
        m_HeadBindRotation=m_Head.rotation;m_AnchorBindRotation=m_Anchor.rotation;
        m_LeftArm=new TrialReviewArm(m_Body,"Left");m_RightArm=new TrialReviewArm(m_Body,"Right");
        m_LeftGrip=m_Left.transform.Find("GripAnchor");m_RightGrip=m_Right.transform.Find("GripAnchor");
        m_EyeRay=new GameObject("Illustrative eye-origin gaze").AddComponent<LineRenderer>();
        m_EyeMaterial=new Material(Shader.Find("Unlit/Color")){color=new Color(1,.65f,.12f)};
        m_EyeRay.sharedMaterial=m_EyeMaterial;m_EyeRay.useWorldSpace=true;m_EyeRay.positionCount=2;
        m_EyeRay.startWidth=.008f;m_EyeRay.endWidth=.004f;m_EyeRay.enabled=false;
    }
    static GameObject Spawn(string resource)
    {
        var prefab=Resources.Load<GameObject>(resource);
        if(prefab==null)throw new InvalidOperationException("Missing replay model: "+resource);
        var instance=UnityEngine.Object.Instantiate(prefab);instance.SetActive(false);return instance;
    }
    public void Prepare(TrialReplayTimeline timeline,double time,bool firstPerson,bool showBody,bool showControllers,bool showEyeRay=false,TrialReviewGazeCorrection correction=null)
    {
        if(m_Timeline!=timeline){m_Timeline=timeline;m_Torso=new TrialReviewTorsoTrack(timeline);}
        m_Torso.Refresh(timeline);
        var sample=timeline.State.HasCurrentSample(time)?timeline.State.Sample:null;
        float torsoYaw;
        bool hasTorso=m_Torso.TryGet(sample,out torsoYaw);
        bool validHead=TrialReplayRenderer.HeadUsable(sample)&&hasTorso;
        m_Body.SetActive(showBody&&!firstPerson&&validHead);
        if(validHead)
        {
            // Seat and torso share this estimated yaw; tracked head rotation stays independent.
            m_Body.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,torsoYaw,0));
            m_Head.rotation=sample.headRotation*Quaternion.Inverse(m_AnchorBindRotation)*m_HeadBindRotation;
            m_Body.transform.position+=sample.headPosition-m_Anchor.position;
            m_ChairBase.rotation=Quaternion.identity; // Stationary base; seat swivels with the illustrative body.
        }
        Controller(m_Left,sample?.leftController,showControllers);
        Controller(m_Right,sample?.rightController,showControllers);
        m_LeftArm.Prepare(m_Body.activeSelf&&m_Left.activeSelf?m_LeftGrip:null,m_Body.transform);
        m_RightArm.Prepare(m_Body.activeSelf&&m_Right.activeSelf?m_RightGrip:null,m_Body.transform);
        bool validGaze=validHead&&TrialReplayRenderer.GazeUsable(sample);
        GazeOriginOffset=validGaze?Vector3.Distance(sample.gazeOrigin,m_Anchor.position):float.NaN;
        var state=timeline.State;
        var readiness=state.Readiness;
        // Match cross visibility: gaze is useful before any charge accumulates, while objects are hidden.
        bool charging=!state.Searching&&TrialReplayState.IsFresh(readiness,time)&&
            (state.PoseBoundary==null||readiness.sequence>state.PoseBoundary.sequence)&&
            readiness.trialId==state.Trial?.trialId&&readiness.readinessActive&&!readiness.readinessPaused;
        Ray corrected=default;
        bool recordedRay=sample!=null&&sample.gazeRayVersion==TrialReplayGaze.Version;
        CorrectionApplied=validGaze&&!recordedRay&&correction!=null&&correction.TryGet(sample,out corrected);
        CorrectedHeadOffset=CorrectionApplied?Vector3.Distance(corrected.origin,sample.headPosition):float.NaN;
        // For a supported recording, missing XR reference must not silently become a cosmetic head ray.
        bool rayAvailable=validGaze&&(recordedRay||correction==null||!correction.Supported||CorrectionApplied);
        GazeAvailable=rayAvailable&&(state.Visible||charging);
        m_EyeRay.enabled=showEyeRay&&GazeAvailable;
        if(GazeAvailable)
        {
            m_EyeRay.startWidth=.008f;m_EyeRay.endWidth=.004f;
            var origin=recordedRay?sample.gazeOrigin:CorrectionApplied?corrected.origin:m_Anchor.position;
            var direction=CorrectionApplied?corrected.direction:sample.gazeDirection.normalized;
            GazeRay=new Ray(origin,direction);
            m_EyeRay.SetPosition(0,origin);
            m_EyeRay.SetPosition(1,origin+direction*3);
        }
    }
    static void Controller(GameObject model,TrialReplayController sample,bool show)
    {
        bool valid=show&&sample!=null&&sample.available&&sample.tracked==1;
        model.SetActive(valid);
        if(valid)model.transform.SetPositionAndRotation(sample.position,sample.rotation);
    }
    public void Dispose()
    {
        UnityEngine.Object.DestroyImmediate(m_EyeRay.gameObject);UnityEngine.Object.DestroyImmediate(m_EyeMaterial);
        UnityEngine.Object.DestroyImmediate(m_Body);UnityEngine.Object.DestroyImmediate(m_Left);UnityEngine.Object.DestroyImmediate(m_Right);
    }
}
