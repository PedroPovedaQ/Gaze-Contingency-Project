using System;
using System.Collections.Generic;

/// <summary>Incremental display index. Gaze spans mean usable observations, never fixations.</summary>
public sealed class TrialReviewTracks
{
    public sealed class Span { public double Start, End; }
    public sealed class Trial
    {
        public TrialReplayRecord Snapshot;
        public double End;
        public bool Completed, TimedOut;
        public readonly List<TrialReplayRecord> Events = new List<TrialReplayRecord>();
        public readonly List<Span> Gaze = new List<Span>();
    }
    public readonly Dictionary<string, Trial> Trials = new Dictionary<string, Trial>();
    public readonly TrialReviewSubtitles Subtitles = new TrialReviewSubtitles();
    public readonly TrialReviewJev Jev = new TrialReviewJev();
    TrialReplayTimeline m_Timeline;
    Trial m_Current;
    Span m_Gaze;
    int m_Next;
    public void Refresh(TrialReplayTimeline timeline)
    {
        if (m_Timeline != timeline) { m_Timeline=timeline; m_Next=0; m_Current=null; m_Gaze=null; Trials.Clear(); Subtitles.Clear(); Jev.Clear(); }
        if (timeline == null) return;
        for (; m_Next<timeline.Records.Count; m_Next++)
        {
            var r=timeline.Records[m_Next];
            Subtitles.Observe(r); Jev.Observe(r);
            if (r.kind=="trial")
            {
                CloseGaze(r.time);
                m_Current=new Trial { Snapshot=r, End=r.time }; Trials[r.trialId]=m_Current;
                continue;
            }
            if (m_Current==null) continue;
            m_Current.End=r.time;
            if (r.kind=="sample" || r.kind=="selection" && r.schema>=2)
            {
                bool usable=r.trialId==m_Current.Snapshot.trialId && TrialReplayRenderer.GazeUsable(r);
                if (!usable) CloseGaze(r.time);
                else
                {
                    if (m_Gaze==null || r.time>m_Gaze.End+1e-7)
                    { m_Gaze=new Span { Start=r.time }; m_Current.Gaze.Add(m_Gaze); }
                    m_Gaze.End=r.time+TrialReplayState.DefaultMaxSampleAgeSeconds;
                }
            }
            if (r.kind!="sample" && r.kind!="header" && r.kind!="mesh") m_Current.Events.Add(r);
            if (r.kind=="selection" && r.correct) m_Current.Completed=true;
            if (r.kind=="trial_timeout") { m_Current.TimedOut=true; CloseGaze(r.time); }
            if (r.kind=="transition" || r.kind=="search_start" || r.kind=="search_resumed" ||
                r.kind=="search_paused" || r.kind=="application_paused" || r.kind=="stop" || r.kind=="end") CloseGaze(r.time);
        }
    }
    void CloseGaze(double time) { if(m_Gaze!=null) m_Gaze.End=Math.Min(m_Gaze.End,time); m_Gaze=null; }
    public Trial For(TrialReplayRecord trial) => trial!=null && Trials.TryGetValue(trial.trialId,out var result)?result:null;
    public static bool Matches(TrialReplayRecord trial,string query,int filter)
    {
        if (filter==1 && trial.practice || filter==2 && !trial.practice) return false;
        query=(query??"").Trim();
        return query.Length==0 || $"{trial.round+1:00} {trial.trialId} {trial.voice} block {trial.block} {(trial.practice?"practice":"trial")} {trial.absoluteTheta}".IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
    }
    public static double AtFraction(double start,double end,float fraction) => start+Math.Max(0,end-start)*Math.Max(0,Math.Min(1,fraction));
}
