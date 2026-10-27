using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// A read-only index: joins request, answer and observed playback by explicit ID, never by text/time guesses.
public sealed class TrialReviewJev
{
    public sealed class Decision
    {
        public string Id, Kind, Application;
        public TrialReplayRecord RequestRecord, ResponseRecord, PlaybackRecord, ApplicationRecord;
        public JObject Request, Response;
        public string Provider => (string)Response?["provider"] ?? (string)Request?["provider"] ?? "jev";
        public bool Legacy;
        public double Time => RequestRecord.time;
        public string TrialId => RequestRecord.trialId;
        public string Status => (string)Response?["status"] ?? "pending";
        public bool Discarded => Application != null && (Application.Contains("stale") || Application.Contains("aborted"));
        public string Summary
        {
            get
            {
                if (Legacy) return "Legacy telemetry · " + Kind;
                if (Discarded) return "Discarded · state changed";
                if (Status != "ok") return Status;
                var answers = Response?["answers"] as JObject;
                return (string)answers?["assistance"]?["choice"] ?? (string)answers?["navigation_action"]?["choice"] ?? (string)answers?["transmit"]?["choice"] ?? (string)answers?["speech_action"]?["choice"] ?? "phrase selected";
            }
        }
    }
    public readonly List<Decision> Decisions = new List<Decision>();
    readonly Dictionary<string, Decision> m_ById = new Dictionary<string, Decision>();
    readonly Dictionary<string, List<TrialReplayRecord>> m_Early = new Dictionary<string, List<TrialReplayRecord>>();
    public void Clear() { Decisions.Clear(); m_ById.Clear(); m_Early.Clear(); }
    public static JObject Detail(TrialReplayRecord record)
    {
        string raw = record.detail ?? "";
        int start = raw.IndexOf('{');
        if (start < 0) return null;
        try { return JObject.Parse(raw.Substring(start)); } catch (Newtonsoft.Json.JsonException) { return null; }
    }
    public static string Field(TrialReplayRecord record, string name)
    {
        foreach (string part in (record.detail ?? "").Split(';'))
            if (part.StartsWith(name + "=", StringComparison.Ordinal)) return part.Substring(name.Length + 1);
        return null;
    }
    public void Observe(TrialReplayRecord record)
    {
        if (record.kind == "sample") return;
        bool trace = record.kind == "jev_trace";
        var data = trace || record.kind == "transmission_decision" || record.kind == "round_encouragement_decision" ? Detail(record) : null;
        string id = trace ? (string)data?["request_id"] : (string)data?["decision_id"] ?? record.guidanceDecisionId ?? Field(record, "decision_id");
        if (trace && (string)data?["stage"] == "request" && !string.IsNullOrEmpty(id))
        {
            if (m_ById.ContainsKey(id)) return;
            var payload = data["payload"] as JObject;
            var questions = payload?["questions"] as JObject;
            if (questions == null) return;
            var names = new List<string>(); foreach (var question in questions.Properties()) names.Add(question.Name);
            var item = new Decision { Id=id, Kind=string.Join(" + ", names), RequestRecord=record, Request=data };
            m_ById.Add(id,item); Decisions.Add(item);
            if (m_Early.TryGetValue(id,out var earlier)) { m_Early.Remove(id); foreach(var entry in earlier) Observe(entry); }
            return;
        }
        if (!string.IsNullOrEmpty(id) && m_ById.TryGetValue(id,out var decision))
        {
            if (trace && (string)data?["stage"] == "response") { decision.Response=data; decision.ResponseRecord=record; }
            else if (trace && (string)data?["stage"] == "application") { decision.Application=(string)data["outcome"]; decision.ApplicationRecord=record; }
            else if (record.kind == "audio_playback_start") decision.PlaybackRecord=record;
            else if (record.kind == "transmission_decision" || record.kind == "round_encouragement_decision")
            { decision.Application=(string)data?["outcome"] ?? (string)data?["status"]; decision.ApplicationRecord=record; }
            else if (record.kind == "interruption_advice" || record.kind == "guidance_decision_aborted")
            { decision.Application=Field(record,"result") ?? Field(record,"reason") ?? record.kind; decision.ApplicationRecord=record; }
            return;
        }
        if (!string.IsNullOrEmpty(id) && (trace || record.kind == "audio_playback_start"))
        {
            if (!m_Early.TryGetValue(id,out var pending)) m_Early[id]=pending=new List<TrialReplayRecord>();
            pending.Add(record);
        }
        // Old recordings remain browsable; never invent their missing candidates/probabilities.
        if (!trace && (record.kind == "transmission_decision" || record.kind == "round_encouragement_decision" || record.kind == "interruption_advice"))
            Decisions.Add(new Decision { Id="legacy:"+record.sequence, Kind=record.kind, RequestRecord=record, Legacy=true, Application=record.detail });
    }
}
