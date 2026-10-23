using System.Collections.Generic;

// Captions follow recorded software speech events, even if WAV transfer is late.
// They are authored phrase text, not a transcription of what the participant heard.
public sealed class TrialReviewSubtitles
{
    readonly List<TrialReplayRecord> m_Changes = new List<TrialReplayRecord>();
    public void Clear() => m_Changes.Clear();
    public void Observe(TrialReplayRecord r)
    {
        if ((r.detail ?? "").Contains("software_one_shot_start") ||
            (r.detail ?? "").StartsWith("success_effect;")) return;
        if (r.kind == "audio_playback_start" || r.kind == "audio_playback_end" ||
            r.kind == "audio_cancelled" || r.kind == "audio_failure" ||
            r.kind == "transition" || r.kind == "search_paused" ||
            r.kind == "application_paused" || r.kind == "stop" || r.kind == "end") m_Changes.Add(r);
    }
    public string At(double time, long sequence)
    {
        int low=0, high=m_Changes.Count;
        while(low<high)
        {
            int mid=low+(high-low)/2; var r=m_Changes[mid];
            if(r.time<time || r.time==time && r.sequence<=sequence) low=mid+1; else high=mid;
        }
        if(low==0)return null;
        var current=m_Changes[low-1];
        return current.kind=="audio_playback_start" && !string.IsNullOrWhiteSpace(current.text)?current.text:null;
    }
}
