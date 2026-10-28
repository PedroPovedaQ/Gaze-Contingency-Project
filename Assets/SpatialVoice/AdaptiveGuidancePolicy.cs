using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    public sealed class AdaptiveGuidanceDecision
    {
        public string Strategy, RequestId, Phrase, PhraseRequestId;
        public bool Speak => !Strategy.StartsWith("observe_", StringComparison.Ordinal);
        public double ReassessSeconds => 1;
    }

    public interface IAdaptiveGuidanceProvider
    {
        Task<AdaptiveGuidanceDecision> ChooseAssistanceAsync(JObject state, IReadOnlyList<string> capabilities, CancellationToken cancellation);
        Task<string> ChooseAssistancePhraseAsync(JObject state, string action, IReadOnlyList<string> phrases, CancellationToken cancellation);
    }

    public static class AdaptiveGuidancePipeline
    {
        public const string WallArrivalOverride = "first_target_wall_entry";
        public static IReadOnlyList<string> WallArrivalPhrases()
        {
            var phrases = new List<string>();
            for (int i=0;i<4;i++) phrases.Add(DirectionalHintPolicy.Variant(DirectionalHintPolicy.AreaPhrase,i));
            return phrases;
        }
        // The verified event fixes the action; the selected provider still chooses the wording.
        public static async Task<AdaptiveGuidanceDecision> ChooseWallArrivalAsync(IAdaptiveGuidanceProvider provider,
            JObject state, IReadOnlyList<string> prepared, Func<bool> current, CancellationToken cancellation)
        {
            var allowed = new List<string>(WallArrivalPhrases());
            var phrases = new List<string>(prepared);
            if (phrases.Count == 0 || phrases.Exists(p => !allowed.Contains(p))) throw new FormatException("Invalid arrival phrases");
            var snapshot = (JObject)state.DeepClone();
            string id = (string)snapshot["decision_id"];
            snapshot["override_reason"] = WallArrivalOverride;
            snapshot["parent_action_decision_id"] = id;
            snapshot["decision_id"] = id + "-phrase";
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new OperationCanceledException();
            string phrase = await provider.ChooseAssistancePhraseAsync(snapshot,"refine_explicit",phrases,cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new OperationCanceledException();
            if (!phrases.Contains(phrase)) throw new FormatException("Unavailable arrival phrase");
            return new AdaptiveGuidanceDecision { Strategy="refine_explicit", RequestId=id,
                PhraseRequestId=id+"-phrase", Phrase=phrase };
        }
        // Await on the caller's context: relevance reads Unity state between both decisions.
        public static async Task<AdaptiveGuidanceDecision> ChooseAsync(IAdaptiveGuidanceProvider provider,
            JObject state, IReadOnlyDictionary<string, IReadOnlyList<string>> options,
            Func<bool> current, CancellationToken cancellation)
        {
            var snapshot = (JObject)state.DeepClone();
            var menus = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var pair in options)
                if (pair.Value != null && pair.Value.Count > 0) menus.Add(pair.Key, new List<string>(pair.Value));
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new OperationCanceledException();
            var decision = await provider.ChooseAssistanceAsync(snapshot, new List<string>(menus.Keys), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new OperationCanceledException();
            if (!decision.Speak) return decision;
            if (!menus.TryGetValue(decision.Strategy, out var phrases)) throw new FormatException("Unavailable action");
            snapshot["parent_action_decision_id"] = decision.RequestId;
            snapshot["decision_id"] = decision.RequestId + "-phrase";
            decision.PhraseRequestId = (string)snapshot["decision_id"];
            decision.Phrase = await provider.ChooseAssistancePhraseAsync(snapshot, decision.Strategy, phrases, cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (!current()) throw new OperationCanceledException();
            if (!new List<string>(phrases).Contains(decision.Phrase)) throw new FormatException("Unavailable phrase");
            return decision;
        }
    }

    public static class AdaptiveGuidanceGrounding
    {
        // Enumerate truthful alternatives, never rank them or choose a preferred variant.
        public static IReadOnlyList<string> Phrases(string strategy, string direction, bool inside,
            float remaining, float verticalFraction, string correction = null, bool progressing = false)
        {
            var phrases = new List<string>();
            void Add(string phrase) { if (!string.IsNullOrEmpty(phrase) && !phrases.Contains(phrase)) phrases.Add(phrase); }
            if (strategy == "correct") { Add(correction); return phrases; }
            if (string.IsNullOrEmpty(direction) || direction == DirectionalHintPolicy.AlignedPhrase) return phrases;
            switch (strategy)
            {
                case "orient_brief":
                case "refine_brief":
                    if (inside != (strategy == "refine_brief")) break;
                    foreach (int i in DirectionalHintPolicy.HasVertical(direction) ? new[] { 0, 1, 2 } : new[] { 0, 1, 3, 7 })
                        Add(DirectionalHintPolicy.Variant(direction, i, verticalFraction: verticalFraction));
                    break;
                case "orient_explicit":
                    if (inside) break;
                    Add(ExplicitOrientation(direction, remaining));
                    Add(DirectionalHintPolicy.Opening(direction));
                    if (remaining > 45)
                        for (int i = 0; i < 4; i++) Add(DirectionalHintPolicy.Turn(direction, remaining, i));
                    break;
                case "refine_explicit":
                    if (inside) for (int i = 0; i < 4; i++) Add(DirectionalHintPolicy.AreaEntry(direction, i, verticalFraction));
                    break;
                case "encourage":
                    if (progressing) for (int i = 0; i < 3; i++) Add(DirectionalHintPolicy.Progress(direction, i));
                    break;
            }
            return phrases;
        }

        public static string ExplicitOrientation(string direction, float remaining) =>
            direction != DirectionalHintPolicy.LeftPhrase && direction != DirectionalHintPolicy.RightPhrase ? null :
            remaining > 45 ? DirectionalHintPolicy.Turn(direction, remaining, 0) : DirectionalHintPolicy.Opening(direction);

        public static string BearingCorrection(string direction, float yaw, float pitch, float verticalFraction, bool suppressVertical) =>
            (!suppressVertical || !DirectionalHintPolicy.HasVertical(direction)) && GazeTargetHistory.StillBeyond(direction, yaw, pitch)
                ? DirectionalHintPolicy.PassedTarget(direction, verticalFraction) : null;

        // A stable reversal invalidates an instruction; adding an axis does not.
        public static bool Opposed(string previous, string current)
        {
            var a = SpeechPhraseMetadata.Describe(previous);
            var b = SpeechPhraseMetadata.Describe(current);
            return (a.horizontal != "none" && b.horizontal != "none" && a.horizontal != b.horizontal) ||
                (a.vertical != "none" && b.vertical != "none" && a.vertical != b.vertical);
        }
    }

    // Observation is a decision, not a failed speech request. There is no forced-speech timer.
    public sealed class AdaptiveGuidanceSchedule
    {
        double m_LastRequest = double.NegativeInfinity, m_Next;
        string m_Event;
        public void Reset() { m_LastRequest = double.NegativeInfinity; m_Next = 0; m_Event = null; }
        public bool Due(double now, string evidenceEvent) => now - m_LastRequest >= .75 && (now >= m_Next || evidenceEvent != m_Event);
        public void Requested(double now, string evidenceEvent) { m_LastRequest = now; m_Event = evidenceEvent; m_Next = now + 2; }
        public void Decided(double now, AdaptiveGuidanceDecision decision) { m_Next = now + decision.ReassessSeconds; }
    }

    // Local, participant-scoped measured history. No demographics, identity or inferred mental state leave this class.
    public sealed class AdaptiveGuidanceHistory
    {
        sealed class Sample { public double Time, Error; public string Direction; }
        readonly List<Sample> m_Samples = new List<Sample>();
        readonly List<JObject> m_Interventions = new List<JObject>();
        readonly Queue<JObject> m_Trials = new Queue<JObject>();
        readonly Dictionary<string, JObject> m_Phrases = new Dictionary<string, JObject>();
        string m_Participant;
        int m_Trial = -1, m_Wrong, m_Cues;
        double m_LastObservation = -1, m_LastSample = -1, m_Active;
        bool m_Ended;
        public int WrongSelections => m_Wrong;
        public bool WallArrivalSpoken { get; private set; }
        public void MarkWallArrivalSpoken() { WallArrivalSpoken=true; }
        public void BeginTrial(string participant, int trial, double now, bool resumed)
        {
            if (participant != m_Participant)
            { m_Trials.Clear(); m_Trial = -1; m_Participant = participant; }
            if (!resumed || trial != m_Trial)
            {
                if (m_Trial >= 0 && !m_Ended) EndTrial("interrupted");
                m_Trial = trial; m_Wrong = m_Cues = 0; m_Active = 0; m_Ended = false;
                m_Interventions.Clear(); m_Phrases.Clear();
                WallArrivalSpoken=false;
            }
            BreakObservation(); m_LastObservation = now;
        }
        public void EndTrial(string outcome)
        {
            if (m_Trial < 0 || m_Ended) return;
            m_Ended = true;
            MarkUnavailable();
            if (m_Trials.Count == 6) m_Trials.Dequeue();
            m_Trials.Enqueue(new JObject { ["trial_index"] = m_Trial, ["outcome"] = outcome,
                ["active_search_seconds"] = Math.Round(m_Active,2), ["wrong_selections"] = m_Wrong,
                ["adaptive_cues_started"] = m_Cues, ["interventions"] = SnapshotInterventions() });
            BreakObservation();
        }
        public void BreakObservation()
        {
            m_Samples.Clear(); m_LastObservation = m_LastSample = -1;
            foreach (var item in m_Interventions)
                if (item["error_reduction_after_3s"].Type == JTokenType.Null) item["response_status"] = "unavailable_discontinuity";
        }
        public void WrongSelection() { m_Wrong++; }
        public void Observe(double now, bool valid, double angularError, string direction)
        {
            double gap = m_LastObservation < 0 ? 0 : now - m_LastObservation;
            if (gap < 0 || gap > 1.25) { BreakObservation(); gap = 0; }
            m_LastObservation = now;
            if (!m_Ended) m_Active += gap;
            if (!valid || double.IsNaN(angularError) || double.IsInfinity(angularError))
            { m_Samples.Clear(); m_LastSample = -1; MarkUnavailable(); return; }
            if (now - m_LastSample < .5) return;
            m_LastSample = now;
            m_Samples.RemoveAll(s => now - s.Time > 12);
            if (m_Samples.Count == 25) m_Samples.RemoveAt(0);
            m_Samples.Add(new Sample { Time = now, Error = angularError, Direction = direction });
            foreach (var item in m_Interventions)
            {
                double age = now - (double)item["at_seconds"];
                if ((string)item["response_status"] != "pending" || age < 3) continue;
                item["response_status"] = age <= 4.25 ? "observed_not_causal" : "unavailable_discontinuity";
                if (age <= 4.25) item["error_reduction_after_3s"] = Math.Round((double)item["error_at_onset"] - angularError,2);
            }
        }
        void MarkUnavailable()
        {
            foreach (var item in m_Interventions)
                if ((string)item["response_status"] == "pending") item["response_status"] = "unavailable_discontinuity";
        }
        public void RecordPhrase(string phrase, string spokenText, string context, double now)
        {
            if (m_Trial < 0 || m_Ended || string.IsNullOrWhiteSpace(phrase)) return;
            if (!m_Phrases.TryGetValue(phrase, out var item))
            {
                item = new JObject { ["phrase"] = phrase, ["count"] = 0 };
                m_Phrases.Add(phrase, item);
            }
            item["count"] = (int)item["count"] + 1;
            item["last_started_seconds"] = now;
            item["spoken_text"] = spokenText;
            item["context"] = context;
        }
        JArray PhraseHistory(double now)
        {
            var result = new JArray();
            foreach (var item in m_Phrases.Values)
            {
                var copy = (JObject)item.DeepClone();
                copy["last_started_age_seconds"] = Math.Round(Math.Max(0, now - (double)copy["last_started_seconds"]), 3);
                copy.Remove("last_started_seconds"); result.Add(copy);
            }
            return result;
        }
        public void Spoken(string id, string strategy, double now, double error, string phrase = null)
        {
            foreach (var item in m_Interventions)
                if ((string)item["response_status"] == "pending") item["response_status"] = "unavailable_overlapping_intervention";
            if (m_Interventions.Count == 8) m_Interventions.RemoveAt(0);
            m_Cues++;
            m_Interventions.Add(new JObject { ["decision_id"] = id, ["strategy"] = strategy, ["at_seconds"] = now,
                ["phrase"] = phrase, ["error_at_onset"] = error, ["error_reduction_after_3s"] = JValue.CreateNull(), ["response_status"] = "pending" });
        }
        JArray SnapshotInterventions() { var a = new JArray(); foreach (var i in m_Interventions) a.Add(i.DeepClone()); return a; }
        public bool HasRecentProgress(string direction, double now)
        {
            if (m_Samples.Count < 2) return false;
            var last = m_Samples[m_Samples.Count-1];
            var previous = m_Samples[m_Samples.Count-2];
            if (now-last.Time > .75 || previous.Error-last.Error <= .1) return false;
            Sample first = last, newer = last;
            for (int i = m_Samples.Count-1; i >= 0; i--)
            {
                var sample = m_Samples[i];
                if (now-sample.Time > 2) break;
                if (sample.Direction != direction || newer.Time-sample.Time > 1.25) return false;
                first = sample; newer = sample;
            }
            return last.Time-first.Time >= .5 && first.Error-last.Error >= 1;
        }
        public string Trend(double now)
        {
            if (m_Samples.Count < 2) return "insufficient";
            var last = m_Samples[m_Samples.Count-1];
            Sample first = last;
            foreach (var s in m_Samples) if (now-s.Time <= 4) { first=s; break; }
            if (last.Time-first.Time < 2) return "insufficient";
            double change=first.Error-last.Error;
            return change > 3 ? "approaching" : change < -3 ? "receding" : "little_change";
        }
        public JObject Snapshot(double now)
        {
            var samples = new JArray(); Sample first = null, last = null; int changes = 0;
            foreach (var s in m_Samples)
            {
                if (now - s.Time > 12) continue;
                if (first == null) first = s;
                if (last != null && last.Direction != s.Direction) changes++;
                last = s;
                samples.Add(new JObject { ["age_seconds"] = Math.Round(now-s.Time,2), ["angular_error_degrees"] = Math.Round(s.Error,2), ["direction"] = s.Direction });
            }
            var trials = new JArray(); foreach (var t in m_Trials) trials.Add(t.DeepClone());
            return new JObject { ["schema"] = "adaptive-behavior-v1", ["trial_index"] = m_Trial,
                ["active_search_seconds"] = Math.Round(m_Active,2), ["wrong_selections"] = m_Wrong,
                ["recent"] = new JObject { ["samples"] = samples, ["direction_changes"] = changes,
                    ["window_seconds"] = first == null ? 0 : Math.Round(last.Time-first.Time,2),
                    ["error_reduction_degrees"] = first == null ? 0 : Math.Round(first.Error-last.Error,2) },
                ["phrase_history"] = PhraseHistory(now), ["interventions"] = SnapshotInterventions(), ["previous_trials"] = trials };
        }
    }
}
