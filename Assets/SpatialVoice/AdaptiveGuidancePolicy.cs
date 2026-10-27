using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    public sealed class AdaptiveGuidanceDecision
    {
        public string Strategy, RequestId;
        public bool Speak => !Strategy.StartsWith("observe_", StringComparison.Ordinal);
        public double ReassessSeconds => Strategy == "observe_4s" ? 4 : Strategy == "observe_2s" ? 2 : 1;
    }

    public interface IAdaptiveGuidanceProvider
    {
        Task<AdaptiveGuidanceDecision> ChooseAssistanceAsync(JObject state, IReadOnlyList<string> capabilities, CancellationToken cancellation);
    }

    public static class AdaptiveGuidanceGrounding
    {
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
        string m_Participant;
        int m_Trial = -1, m_Wrong, m_Cues;
        double m_LastObservation = -1, m_LastSample = -1, m_Active;
        bool m_Ended;
        public int WrongSelections => m_Wrong;
        public void BeginTrial(string participant, int trial, double now, bool resumed)
        {
            if (participant != m_Participant)
            { m_Trials.Clear(); m_Trial = -1; m_Participant = participant; }
            if (!resumed || trial != m_Trial)
            {
                if (m_Trial >= 0 && !m_Ended) EndTrial("interrupted");
                m_Trial = trial; m_Wrong = m_Cues = 0; m_Active = 0; m_Ended = false;
                m_Interventions.Clear();
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
        public void Spoken(string id, string strategy, double now, double error)
        {
            foreach (var item in m_Interventions)
                if ((string)item["response_status"] == "pending") item["response_status"] = "unavailable_overlapping_intervention";
            if (m_Interventions.Count == 8) m_Interventions.RemoveAt(0);
            m_Cues++;
            m_Interventions.Add(new JObject { ["decision_id"] = id, ["strategy"] = strategy, ["at_seconds"] = now,
                ["error_at_onset"] = error, ["error_reduction_after_3s"] = JValue.CreateNull(), ["response_status"] = "pending" });
        }
        JArray SnapshotInterventions() { var a = new JArray(); foreach (var i in m_Interventions) a.Add(i.DeepClone()); return a; }
        public bool HasRecentProgress(string direction, double now)
        {
            if (m_Samples.Count < 2) return false;
            var last = m_Samples[m_Samples.Count-1];
            var previous = m_Samples[m_Samples.Count-2];
            return now-last.Time <= .75 && last.Direction == direction && previous.Direction == direction &&
                last.Time-previous.Time <= 1 && previous.Error-last.Error >= 1;
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
                ["interventions"] = SnapshotInterventions(), ["previous_trials"] = trials };
        }
    }
}
