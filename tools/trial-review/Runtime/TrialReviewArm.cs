using System.Linq;
using UnityEngine;

/// <summary>Display-only two-bone IK. Controller records are never adjusted to fit the avatar.</summary>
public sealed class TrialReviewArm
{
    readonly Transform m_Upper,m_Fore,m_Hand,m_Palm;
    readonly Quaternion m_UpperRest,m_ForeRest,m_HandRest;
    readonly Vector3 m_ForePosition,m_HandPosition;
    readonly float m_Side;
    public Transform Palm => m_Palm;
    public bool Following { get; private set; }
    public bool ReachLimited { get; private set; }
    public TrialReviewArm(GameObject body,string side)
    {
        var bones=body.GetComponentsInChildren<Transform>(true);
        m_Upper=bones.Single(t=>t.name==side+"Arm");m_Fore=bones.Single(t=>t.name==side+"ForeArm");
        m_Hand=bones.Single(t=>t.name==side+"Hand");m_Palm=bones.Single(t=>t.name==side+"PalmAnchor");
        m_UpperRest=m_Upper.localRotation;m_ForeRest=m_Fore.localRotation;m_HandRest=m_Hand.localRotation;
        m_ForePosition=m_Fore.localPosition;m_HandPosition=m_Hand.localPosition;m_Side=side=="Left"?-1:1;
    }
    public void Prepare(Transform grip,Transform body)
    {
        // Reset before every solve: backward seeks and invalid tracking cannot retain an old arm pose.
        m_Upper.localRotation=m_UpperRest;m_Fore.localRotation=m_ForeRest;m_Hand.localRotation=m_HandRest;
        m_Fore.localPosition=m_ForePosition;m_Hand.localPosition=m_HandPosition;
        Following=grip!=null;ReachLimited=false;if(!Following)return;
        var handRotation=grip.rotation*Quaternion.Inverse(m_Palm.localRotation);
        var wrist=grip.position-handRotation*Vector3.Scale(m_Palm.localPosition,m_Hand.lossyScale);
        var shoulder=m_Upper.position;var displacement=wrist-shoulder;float distance=displacement.magnitude;
        float upper=Vector3.Distance(shoulder,m_Fore.position),lower=Vector3.Distance(m_Fore.position,m_Hand.position);
        // Cartoon proportions differ from participants. Limited visual length adaptation keeps hands attached
        // over ordinary reach; outliers are clamped and disclosed instead of moving recorded controllers.
        float stretch=Mathf.Clamp(distance/(.98f*(upper+lower)),1,1.5f);
        m_Fore.localPosition=m_ForePosition*stretch;m_Hand.localPosition=m_HandPosition*stretch;
        upper*=stretch;lower*=stretch;
        float solved=Mathf.Clamp(distance,Mathf.Abs(upper-lower)+.0001f,upper+lower-.0001f);
        ReachLimited=Mathf.Abs(solved-distance)>.001f;
        var direction=distance>.00001f?displacement/distance:body.forward;
        var pole=Vector3.ProjectOnPlane(body.right*m_Side*.5f+Vector3.down,direction);
        if(pole.sqrMagnitude<.00001f)pole=Vector3.ProjectOnPlane(body.forward,direction);
        float along=(upper*upper-lower*lower+solved*solved)/(2*solved);
        var elbow=shoulder+direction*along+pole.normalized*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
        Point(m_Upper,m_Fore,elbow-shoulder);
        Point(m_Fore,m_Hand,shoulder+direction*solved-m_Fore.position);
        m_Hand.rotation=handRotation;
    }
    static void Point(Transform bone,Transform child,Vector3 direction)
    { bone.rotation=Quaternion.FromToRotation(child.position-bone.position,direction)*bone.rotation; }
}
