using System;
using System.Linq;
using UnityEngine;

/// <summary>Versioned, exact-recording recovery of the legacy position-only X reflection.
/// Returns a derived ray; never changes recorded fields or substitutes the head position.</summary>
public sealed class TrialReviewGazeCorrection
{
    [Serializable] public sealed class Profile
    { public string id,recordingId,buildGuid,sha256,method,evidence; }
    [Serializable] sealed class Catalog { public Profile[] profiles; }
    public const string Method="reflect_origin_across_recorded_xr_local_x_v1";
    public Profile AppliedProfile { get; }
    public bool Supported => AppliedProfile!=null;
    public TrialReviewGazeCorrection(TrialReplayTimeline timeline,string hash)
    {
        var asset=Resources.Load<TextAsset>("GazeCorrectionProfiles");
        if(asset==null)throw new InvalidOperationException("Missing gaze correction profiles");
        AppliedProfile=JsonUtility.FromJson<Catalog>(asset.text).profiles.SingleOrDefault(p=>
            p.sha256==hash&&p.recordingId==timeline.Header.recordingId&&p.buildGuid==timeline.Header.buildGuid&&p.method==Method);
    }
    public bool TryGet(TrialReplayRecord sample,out Ray ray)
    {
        ray=default;
        if(!Supported||!TrialReplayRenderer.GazeUsable(sample)||!sample.xrOriginAvailable||
            !Finite(sample.gazeOrigin)||!Finite(sample.gazeDirection)||!Finite(sample.xrOriginPosition))return false;
        Quaternion rotation=sample.xrOriginRotation;
        float norm=Quaternion.Dot(rotation,rotation);
        if(float.IsNaN(norm)||float.IsInfinity(norm)||Mathf.Abs(norm-1)>.01f)return false;
        Vector3 corrected=UnmirrorOrigin(sample.gazeOrigin,sample.xrOriginPosition,rotation.normalized);
        ray=new Ray(corrected,sample.gazeDirection.normalized);
        return true;
    }
    // In recorded coordinates: c = o + R * mirrorX(inverse(R) * (g - o)).
    // The historical bug changed localPosition.x, not rotation: DO NOT reflect the direction.
    public static Vector3 UnmirrorOrigin(Vector3 gaze,Vector3 origin,Quaternion rotation)
    {
        var local=Quaternion.Inverse(rotation)*(gaze-origin);local.x=-local.x;
        return origin+rotation*local;
    }
    static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&
        !float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
}
