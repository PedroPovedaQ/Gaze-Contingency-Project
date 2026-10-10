using System.Collections.Generic;
using UnityEngine;

/// <summary>Display-only torso estimate; never exported as a participant measurement.
/// A neck dead zone allows head-only turns. Larger turns lead the torso/seat with
/// a bounded rate. Precomputed in source order so scrubbing cannot change the pose.</summary>
public sealed class TrialReviewTorsoTrack
{
    public const float NeckYawAllowance = 35f, MaxTurnDegreesPerSecond = 90f;
    readonly Dictionary<long,float> m_Yaws = new Dictionary<long,float>();
    readonly TrialReplayState state = new TrialReplayState();
    bool continuous; double lastTime; float yaw;
    int processed;
    public TrialReviewTorsoTrack(TrialReplayTimeline timeline) { Refresh(timeline); }
    public void Refresh(TrialReplayTimeline timeline)
    {
        for (; processed < timeline.Records.Count; processed++)
        {
            var record = timeline.Records[processed];
            state.Apply(record);
            if(state.PoseBoundary==record)continuous=false;
            if(state.Sample!=record)continue;
            Vector3 forward=record.headRotation*Vector3.forward;
            if(!state.HasCurrentSample(record.time)||!TrialReplayRenderer.HeadUsable(record)||forward.x*forward.x+forward.z*forward.z<.0001f)
            {continuous=false;continue;}
            float headYaw=Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg;
            double dt=record.time-lastTime;
            if(!continuous||dt<0||dt>TrialReplayState.DefaultMaxSampleAgeSeconds)yaw=headYaw;
            else
            {
                float difference=Mathf.DeltaAngle(yaw,headYaw);
                if(Mathf.Abs(difference)>NeckYawAllowance)
                {
                    float desired=headYaw-Mathf.Sign(difference)*NeckYawAllowance;
                    yaw=Mathf.MoveTowardsAngle(yaw,desired,MaxTurnDegreesPerSecond*(float)dt);
                }
            }
            m_Yaws[record.sequence]=yaw;lastTime=record.time;continuous=true;
        }
    }
    public bool TryGet(TrialReplayRecord sample,out float yaw)
    {
        yaw=0;return sample!=null&&m_Yaws.TryGetValue(sample.sequence,out yaw);
    }
}
