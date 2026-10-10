using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class TrialReviewInterfaceChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception("Replay interface: "+message);}
    public static void Run(TrialReplayTimeline timeline)
    {
        var tracks=new TrialReviewTracks();tracks.Refresh(timeline);
        var first=tracks.For(timeline.Trials[0]);
        Check(tracks.Trials.Count==3&&first.Completed,"Trial index and completion come from records");
        Check(!first.Gaze.Any(s=>s.Start<4.1&&s.End>4.1),"Invalid gaze must leave a gap");
        Check(!first.Gaze.Any(s=>s.Start<8.3&&s.End>8.3),"Missing samples must leave a gap");
        Check(first.Events.Any(r=>r.kind=="audio_cancelled"),"Audio cancellation remains navigable");
        int count=first.Gaze.Count;tracks.Refresh(timeline);Check(first.Gaze.Count==count,"Repeated refresh does not duplicate spans");
        Check(TrialReviewTracks.Matches(timeline.Trials[0],"NEUTRAL",1),"Case-insensitive voice search");
        Check(!TrialReviewTracks.Matches(timeline.Trials[0],"",2),"Practice filter excludes study trials");
        Check(TrialReviewTracks.Matches(new TrialReplayRecord{practice=true,voice="self_similar"},"self",2),"Practice voice search");
        Check(!TrialReviewTracks.Matches(timeline.Trials[0],"does-not-exist",0),"Unmatched query has no results");
        Check(TrialReviewTracks.AtFraction(20,40,.25f)==25&&TrialReviewTracks.AtFraction(20,40,2)==40,"Timeline maps relative cursor and clamps");
        string root=Path.Combine(Path.GetTempPath(),"trial-review-export-check-"+Guid.NewGuid().ToString("N"));
        try
        {
            string hash=TrialReviewSummary.Hash(timeline.FilePath);
            string folder=TrialReviewExport.Write(root,timeline,timeline.Trials[0],hash,null);
            var samples=TrialReviewSummary.Parse(File.ReadAllText(Path.Combine(folder,"samples.csv")));
            var events=TrialReviewSummary.Parse(File.ReadAllText(Path.Combine(folder,"events.csv")));
            Check(samples.Count>100&&samples.All(r=>r.Count==samples[0].Count),"Export samples CSV is rectangular including quoted JSON");
            Check(samples.Where(r=>r[3]=="selection").All(r=>r[7]==""&&r[13]==""),"Unavailable selection poses export blank coordinates");
            Check(events.All(r=>r.Count==events[0].Count)&&events.Any(r=>r.Contains("audio_cancelled")),"Event CSV round-trips cancellation and raw fields");
            Check(!File.Exists(Path.Combine(folder,"final-summary.csv")),"No fabricated final summary");
            Check(File.ReadAllLines(Path.Combine(folder,"observations.jsonl")).All(line=>!line.Contains("demo-1")),"Export does not leak the next trial");
            Check(TrialReviewSummary.Hash(timeline.FilePath)==hash,"Export does not modify source recording");
            var summary=new System.Collections.Generic.Dictionary<string,string>{{"trial_id",timeline.Trials[0].trialId},{"comment","quoted \"voice\",\nsecond line"}};
            string withSummary=TrialReviewExport.Write(root,timeline,timeline.Trials[0],hash,summary);
            var roundTrip=TrialReviewSummary.Parse(File.ReadAllText(Path.Combine(withSummary,"final-summary.csv")));
            Check(roundTrip[1][1]==summary["comment"],"Supplied final summary preserves quotes/newlines");
            bool rejected=false;try{TrialReviewExport.Write(root,timeline,timeline.Trials[0],"wrong-hash",null);}catch(IOException){rejected=true;}
            Check(rejected,"Changed source cannot export under an old identity");
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        Debug.Log("TRIAL_REVIEW_INTERFACE_PASS trial search/filter, gaps, event navigation data, cursor math, CSV export identity and provenance");
    }
}
