using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class TrialReviewApp
{
    string m_SelectedJev;
    Vector2 m_JevScroll;
    static Color JevColor(TrialReviewJev.Decision d)
    {
        if (d.Discarded) return new Color(.65f,.65f,.7f);
        if (d.Status == "timeout" || d.Status == "error") return new Color(.95f,.35f,.32f);
        if (d.Summary == "fade_out") return new Color(1,.6f,.2f);
        if (d.Summary == "speak" || d.PlaybackRecord != null) return new Color(.3f,.8f,.55f);
        return new Color(.6f,.63f,.68f);
    }
    void SelectJev(TrialReviewJev.Decision d)
    { m_SelectedJev=d.Id; m_Tab=3; m_Playing=false; m_JevScroll=Vector2.zero; Seek(d.Time,d.RequestRecord.sequence); }
    bool SelectJevAtPointer(Rect rect,double start,double end,TrialReplayRecord trial,Vector2 pointer)
    {
        if(pointer.y<rect.y+109 || pointer.y>rect.y+129)return false;
        foreach(var d in m_Tracks.Jev.Decisions)
        {
            if(d.Time<start || d.Time>end || (!m_RunTimeline && d.TrialId!=trial?.trialId))continue;
            float x=rect.x+(float)((d.Time-start)/Math.Max(.001,end-start))*rect.width;
            if(new Rect(Mathf.Min(rect.xMax-9,x-3),rect.y+109,9,20).Contains(pointer)) { SelectJev(d);return true; }
        }
        return false;
    }
    void JevTrack(Rect rect,double start,double end,TrialReplayRecord trial)
    {
        foreach (var d in m_Tracks.Jev.Decisions)
        {
            if (d.Time<start || d.Time>end || (!m_RunTimeline && d.TrialId!=trial?.trialId)) continue;
            float x=rect.x+(float)((d.Time-start)/Math.Max(.001,end-start))*rect.width;
            var marker=new Rect(Mathf.Min(rect.xMax-9,x-3),rect.y+109,9,20);
            var color=JevColor(d);
            if(d.Discarded) { Fill(marker,color); Fill(new Rect(marker.x+2,marker.y+2,marker.width-4,marker.height-4),m_TrackBackground); }
            else Fill(marker,color);
            if(GUI.Button(marker,new GUIContent("",$"{d.Provider} · {d.Kind}\n{d.Summary}\nClick for state, alternatives, response and playback."),GUIStyle.none))SelectJev(d);
        }
    }
    void JevInspector(TrialReplayRecord trial)
    {
        m_JevScroll=GUILayout.BeginScrollView(m_JevScroll);
        var decisions=m_Tracks.Jev.Decisions.Where(d=>d.TrialId==trial?.trialId).ToList();
        GUILayout.Label("MODEL DECISIONS",m_Heading);
        GUILayout.Label($"{decisions.Count(d=>!d.Legacy)} recorded requests · {decisions.Count(d=>d.Legacy)} legacy events",m_MutedLabel);
        GUILayout.Label("Recorded history, including outcomes after the cursor. Select a request to inspect the scene when it was sent.",m_MutedLabel);
        foreach(var d in decisions)
        {
            string timing=d.Response?["elapsed_ms"] != null ? $" · {(double)d.Response["elapsed_ms"]:0} ms" : "";
            if(GUILayout.Button(new GUIContent($"{Clock(d.Time-(trial?.time??0))} · {d.Provider} · {d.Summary}{timing}",d.Kind+"\nClick to inspect the recorded request."),m_SelectedJev==d.Id?m_ActiveRow:m_Row))SelectJev(d);
        }
        var selected=decisions.FirstOrDefault(d=>d.Id==m_SelectedJev);
        if(selected==null && decisions.Count>0)
        {
            selected=decisions.LastOrDefault(d=>d.Time<=m_Time) ?? decisions[0];
            m_SelectedJev=selected.Id;
        }
        if(selected==null) { GUILayout.Label("Select a marker or decision above. Older runs may lack full Jev traces.",m_Label); GUILayout.EndScrollView(); return; }
        GUILayout.Space(12);
        GUILayout.Label(selected.Kind,m_Heading);
        if(selected.Legacy)
        { GUILayout.Label("Candidates and probabilities: not recorded in this version.",m_Label);GUILayout.Label(selected.Application??"No details",m_Code);GUILayout.EndScrollView();return; }
        GUILayout.Label("PROVIDER AND PLAYBACK",m_Heading);
        GUILayout.Label("Provider: "+selected.Provider+" · "+selected.Status,m_Label);
        GUILayout.Label("Unity: "+(selected.Application??"No application outcome recorded"),m_Label);
        GUILayout.Label("Model version: " + ((string)selected.Response?["model_version"] ?? (string)selected.Request?["model_version"] ?? "Not recorded"),m_Label);
        if (selected.Response?["probability_semantics"] != null)
            GUILayout.Label("Probability semantics: " + (string)selected.Response["probability_semantics"],m_MutedLabel);
        var playback=selected.PlaybackRecord;
        GUILayout.Label(playback==null?"Playback start not recorded for this request.":$"Playback started {Clock(playback.time-(trial?.time??0))}\n{playback.text}\nRequest to playback: {(playback.time-selected.Time)*1000:0} ms",m_Label);
        var payload=selected.Request?["payload"] as JObject;
        bool phraseStage = payload?["questions"]?["assistance_phrase"] != null;
        bool adaptive = payload?["questions"]?["assistance"] != null || phraseStage;
        if (phraseStage)
        {
            string parentId = (string)payload?["state"]?["parent_action_decision_id"];
            GUILayout.Label("Phrase stage · Action: " + (string)payload?["state"]?["selected_action"], m_Heading);
            GUILayout.Label("Round phrase history is included in the frozen state below. Playback is linked to the parent action request.", m_MutedLabel);
            var parent = decisions.FirstOrDefault(d => d.Id == parentId);
            if (parent != null && GUILayout.Button("View parent action and playback", m_Row)) SelectJev(parent);
        }
        GUILayout.Label(adaptive
            ? "Adaptive policy: observation stays silent; unavailable advice is retried without an automatic search cue. Playback marks software onset."
            : "Playback is a recorded software onset, not proof the participant heard it. A timeout can still produce local fallback audio.",m_MutedLabel);
        if (adaptive && selected.ApplicationRecord != null)
            GUILayout.Label(TrialReviewJev.Detail(selected.ApplicationRecord)?.ToString(Formatting.Indented) ?? selected.Application, m_Code);
        var questions=payload?["questions"] as JObject;
        var answers=selected.Response?["answers"] as JObject;
        if(questions!=null)foreach(var q in questions.Properties())
        {
            GUILayout.Space(10);GUILayout.Label(q.Name,m_Heading);
            var answer=answers?[q.Name];
            string choice=(string)answer?["choice"];
            GUILayout.Label("Selected: "+(choice??"No accepted answer recorded"),m_Label);
            if(q.Value["criteria"] is JObject criteria)foreach(var option in criteria.Properties())
            {
                string probability=answer?["probabilities"]?[option.Name]?.ToString();
                bool numeric=double.TryParse(probability,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var value) && value>=0 && value<=1;
                string suffix=numeric?$" ({value:P0})":" (probability not recorded)";
                GUILayout.Label(new GUIContent((option.Name==choice?"✓ ":"")+option.Name+suffix,option.Value.ToString(Formatting.Indented)),m_Label);
                if(numeric) { var bar=GUILayoutUtility.GetRect(20,5,GUILayout.ExpandWidth(true));Fill(bar,m_TrackBackground);Fill(new Rect(bar.x,bar.y,bar.width*(float)value,bar.height),m_Accent); }
            }
        }
        GUILayout.Space(10);GUILayout.Label("STATE SENT TO MODEL",m_Heading);
        GUILayout.Label(new GUIContent(payload?["state"]?.ToString(Formatting.Indented)??"Not recorded","Frozen state at request time, not the current playback cursor. Null means unavailable."),m_Code);
        GUILayout.Label("Model probabilities describe the supplied alternatives; they are not validated measures of navigation correctness.",m_MutedLabel);
        GUILayout.EndScrollView();
    }

    // One consistent hover layer also makes existing GUIContent tooltips visible.
    static string Help(string label)
    {
        switch(label)
        {
            case "At cursor": return "Recorded gaze, controller and object state at the current timeline position.";
            case "Events": return "Recorded events for this trial. Click an entry to seek to it.";
            case "Final data": return "Final performance and survey values, not measurements at the cursor.";
            case "Jev": return "Requests, allowed answers, model probabilities and observed playback. Historical gaps remain unknown.";
            case "Evaluation": return "Automatic evidence checks and separate Astra judgments. Model judgments are not participant ratings or ground truth.";
            case "Human model": return "Illustrative seated body fixed at the trial center. Head rotation and controller poses animate; the chair does not follow head translation. Not measured full-body tracking.";
            case "Walls": return "Show reconstructed search walls; this changes the viewer only.";
            case "Degrees": return "Show world-angle labels to inspect recorded turning and target placement.";
            case "Room": return "Show the illustrative room environment, not recorded passthrough video.";
            case "Reveal target": return "Reveal the target to the researcher in replay. This does not change what the participant saw.";
            case "Before alignment": case "Raw gaze": return "Show the available pre-alignment gaze ray for comparison; historical data is unchanged.";
            case "Corrected gaze": case "Eye ray (display)": return "Legacy gaze reconstruction. In third person, the visible line connects the headset to the analytical ray endpoint; analysis keeps its original origin and direction. Check provenance before interpreting accuracy.";
            case "Detection ring": return "Target-vicinity overlay for inspection. Eligibility still depends on the recorded closest-object and validity rules.";
            case "State markers": return "Labels captured direct gaze, vicinity winner and right-controller hits. Hidden for missing, stale or invalid state; never inferred from legacy geometry.";
            case "Subtitles": return "Show recorded speech text at the playhead, including when a saved audio file is missing.";
            case "Follow live": return "Follow the newest buffered headset recording. Scrubbing leaves live-follow mode.";
            case "Whole session": case "Selected trial": return "Switch timeline range between the complete recording and the current trial.";
            case "‹ Sample": case "Sample ›": return "Step to the previous or next recorded sample; no motion is invented between missing observations.";
            case "‹ Event": case "Event ›": return "Jump to the previous or next recorded event.";
            case "Mute": case "Unmute": case "Mute voice": case "Unmute voice": return "Control this viewer's voice/effect audio only. Volume and mute are saved. Headset audio and recordings are unchanged.";
            case "Play": case "Pause": return "Play or pause this reconstruction. Space also toggles playback; saved audio plays at 1×.";
            case "First person": return "View through the recorded head pose.";
            case "Third person": return "Orbit around the reconstructed participant.";
            case "Overhead": return "Inspect the room layout and turning from above.";
            case "Gaze": case "Eye gaze": return "Recorded gaze ray. Third person connects the headset to its endpoint for display; analysis and first person retain the recorded origin and direction. A ray intersection is not a validated fixation.";
            case "Controller": case "Controllers": return "Recorded controller pose and ray; selection requires the recorded trigger event.";
            case "Vicinity": return "Inspect the target-vicinity region. This viewer overlay does not change recorded decisions.";
            case "Open run…": return "Open a saved recording.jsonl with its audio folder.";
            case "Export trial data…": return "Export this recorded trial without modifying its source recording.";
            default: return label.StartsWith("Export")?"Export recorded data; source files stay unchanged.":null;
        }
    }
    static GUIContent HelpContent(string label) => new GUIContent(label,Help(label));
    void DrawHoverHelp(float width,float height)
    {
        if(string.IsNullOrEmpty(GUI.tooltip))return;
        var content=new GUIContent(GUI.tooltip);
        float w=360,h=Mathf.Min(260,m_Label.CalcHeight(content,w-24)+20);
        var mouse=Event.current.mousePosition;
        var box=new Rect(Mathf.Clamp(mouse.x+16,8,width-w-8),Mathf.Clamp(mouse.y+22,8,height-h-8),w,h);
        bool enabled=GUI.enabled;GUI.enabled=true;
        Fill(box,m_Border);Fill(new Rect(box.x+1,box.y+1,box.width-2,box.height-2),m_Surface);
        GUI.Label(new Rect(box.x+12,box.y+10,w-24,h-20),content,m_Label);GUI.enabled=enabled;
    }
}
