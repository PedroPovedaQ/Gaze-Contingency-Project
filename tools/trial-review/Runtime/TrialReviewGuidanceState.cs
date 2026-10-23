using System;
using System.Collections.Generic;

/// <summary>Read-only cursor index of captured state. Never infers a state from reconstructed geometry.</summary>
public sealed class TrialReviewGuidanceState
{
    readonly List<TrialReplayRecord> m_Records=new List<TrialReplayRecord>();
    TrialReplayTimeline m_Timeline;
    int m_Indexed;
    public GuidanceStateSnapshot Current { get; private set; }
    public string Availability { get; private set; }="No current sample";

    public void Prepare(TrialReplayTimeline timeline,double time,long sequence,bool waiting)
    {
        Current=null;
        if(m_Timeline!=timeline){m_Timeline=timeline;m_Indexed=0;m_Records.Clear();}
        if(timeline==null){Availability="No recording";return;}
        while(m_Indexed<timeline.Records.Count)
        {
            var record=timeline.Records[m_Indexed++];
            if(IsRecorded(record.guidanceState))m_Records.Add(record);
        }
        if(waiting){Availability="Waiting for headset";return;}
        int low=0,high=m_Records.Count;
        while(low<high)
        {
            int mid=(low+high)/2;var r=m_Records[mid];
            if(r.time<time||r.time==time&&r.sequence<=sequence)low=mid+1;else high=mid;
        }
        var latest=low>0?m_Records[low-1]:null;
        // Lifecycle events remain meaningful after sampling stops. Spatial hits do
        // not: markers and geometry still require a current tracked sample.
        if(latest!=null&&latest.trialId==timeline.State.Trial?.trialId&&
            Lifecycle(latest.guidanceState.phase))
        {Current=latest.guidanceState;Availability="Recorded lifecycle state";return;}
        if(!timeline.State.HasCurrentSample(time)){Availability="No current sample";return;}
        if(latest==null||latest.trialId!=timeline.State.Trial?.trialId||
            latest.sequence<timeline.State.Sample.sequence)
        {Availability="State not recorded (legacy / missing)";return;}
        Current=latest.guidanceState;
        Availability="Recorded at cursor";
    }

    // JsonUtility expands null inline serializable objects into default-valued
    // nested objects. Version alone can therefore exist without a captured state.
    public static bool IsRecorded(GuidanceStateSnapshot state) => state!=null&&!string.IsNullOrWhiteSpace(state.phase);
    static bool Lifecycle(string phase) => phase=="Opening"||phase=="Paused"||phase=="Completed"||
        phase=="Stopped"||phase=="Inactive";

    public static string Hit(string id,bool valid) => !valid?"Unavailable / invalid":string.IsNullOrEmpty(id)?"None":id;
    public static string Transition(TrialReplayRecord record)
    {
        var state=record.guidanceState;
        return !IsRecorded(state)||record.kind!="guidance_state"?record.text??record.detail:
            $"{state.previousPhase} → {state.phase} · {state.transitionReason}";
    }
}
