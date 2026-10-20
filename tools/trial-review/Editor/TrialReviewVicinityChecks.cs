using System;
using UnityEngine;

public static class TrialReviewVicinityChecks
{
    static void Assert(bool value,string message){if(!value)throw new Exception("Vicinity: "+message);}
    public static void Run(TrialReplayTimeline timeline)
    {
        using(var renderer=new TrialReplayRenderer(timeline))
        using(var overlay=new TrialReviewVicinity())
        {
            renderer.Prepare(timeline,1);
            var origin=timeline.State.Sample.headPosition;
            overlay.Prepare(timeline,renderer,1,long.MaxValue,false,new Ray(origin,Vector3.forward),true);
            var target=renderer.Objects[timeline.State.Trial.targetId];
            var ray=new Ray(origin,target.GetComponent<Collider>().bounds.center-origin);
            overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,true);
            Assert(overlay.WinnerId==target.name&&overlay.RingVisible,"center gaze wins and target ring is visible");
            var material=target.GetComponent<Renderer>().sharedMaterial;
            Assert(material.shader.name=="TrialReview/ObjectShine","shine shader loaded");
            Assert(material.GetColor("_RimColor")==new Color(.7f,.87f,1),"right controller keeps blue rim");
            string serialized=JsonUtility.ToJson(timeline.State.Sample);
            overlay.Prepare(timeline,renderer,1,long.MaxValue,false,ray,true);
            Assert(overlay.WinnerId==null&&!overlay.RingVisible,"invalid gaze clears ring and winner");
            Assert(serialized==JsonUtility.ToJson(timeline.State.Sample),"overlay preserves recorded hits");
            overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,false);
            Assert(overlay.WinnerId==target.name&&!overlay.RingVisible,"ring visibility does not disable gaze shine");
            renderer.Prepare(timeline,8.2);
            overlay.Prepare(timeline,renderer,8.2,long.MaxValue,true,ray,true);
            Assert(overlay.WinnerId==null&&!overlay.RingVisible,"no gaze shine across stale gaps");
            renderer.Prepare(timeline,21);
            overlay.Prepare(timeline,renderer,21,long.MaxValue,true,ray,true);
            renderer.Prepare(timeline,1);
            overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,true);
            Assert(overlay.WinnerId==timeline.State.Trial.targetId&&overlay.RingVisible,"backward trial seek rebuilds colliders");
        }
        var a=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var b=GameObject.CreatePrimitive(PrimitiveType.Sphere);
        try
        {
            a.transform.position=new Vector3(.05f,0,3);b.transform.position=new Vector3(-.05f,0,3);
            a.transform.localScale=b.transform.localScale=Vector3.one*.1f;Physics.SyncTransforms();
            var objects=new[]{a,b};
            var tie=TrialReviewVicinityPolicy.ClosestObject(Vector3.zero,Vector3.forward,objects,6,out int count,out bool ambiguous,out _);
            Assert(tie==null&&count==2&&ambiguous,"overlapping equal-distance rings have no winner");
            var closer=TrialReviewVicinityPolicy.ClosestObject(Vector3.zero,a.transform.position,objects,6,out count,out ambiguous,out _);
            Assert(closer==a&&count==2&&!ambiguous,"closest angular center wins overlapping rings");
            Assert(TrialReviewVicinityPolicy.ClosestObject(Vector3.zero,Vector3.back,objects,6,out _,out _,out _)==null,"gaze behind objects has no winner");
        }
        finally {UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        Debug.Log("TRIAL_REVIEW_VICINITY_PASS overlap, ambiguity, invalid/stale gaze, ring toggle, controller rim, backward seek, immutable source");
    }
}
