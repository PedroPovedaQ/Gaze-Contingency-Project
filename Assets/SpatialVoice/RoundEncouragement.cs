using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    /// <summary>Only observed round facts. No participant identity, eye traces, voice condition or inferred feelings.</summary>
    [Serializable]
    public sealed class RoundPerformance
    {
        public bool measurementsAvailable, practice;
        public double searchSeconds;
        public int incorrectSelections, guidanceStarts, repeatRequests;
        public int? plannedAbsoluteTurnDegrees;
        public int? trialNumber, blockNumber, trialInBlock;
        public string previousFeedback;
    }

    public sealed class EncouragementPhrase
    {
        public readonly string id, text, evidence;
        public EncouragementPhrase(string id, string text, string evidence)
        { this.id = id; this.text = text; this.evidence = evidence; }
    }

    public static class RoundEncouragement
    {
        public const string Version = "round-encouragement-v2";
        public const int DeadlineMilliseconds = 600;
        // Pilot wording rule, not a validated speed norm or comparison with other people.
        public const double QuickSearchSeconds = 10;
        public static bool IsQuick(RoundPerformance facts) => facts != null && facts.measurementsAvailable &&
            !facts.practice && facts.incorrectSelections == 0 && facts.searchSeconds > 0 && facts.searchSeconds <= QuickSearchSeconds;
        public static readonly EncouragementPhrase[] Phrases = {
            new EncouragementPhrase("nice", "You selected the right object. Nice work.", "Correct trigger selection completed the round."),
            new EncouragementPhrase("well_done", "That's the right object. Well done.", "Correct trigger selection completed the round."),
            new EncouragementPhrase("got_it", "You got it. Round complete.", "Correct trigger selection completed the round."),
            new EncouragementPhrase("nicely_done", "Correct selection. Nicely done.", "Correct trigger selection completed the round."),
            new EncouragementPhrase("first_selection", "Nice work. You found it on your first selection.", "Zero incorrect selections, not necessarily the first object looked at."),
            new EncouragementPhrase("first_selection_short", "First selection, right object. Nicely done.", "Zero incorrect selections; no claim about speed or gaze accuracy."),
            new EncouragementPhrase("recovery", "You found it. Nice recovery.", "Correct selection after one or more incorrect selections."),
            new EncouragementPhrase("recovery_persistence", "You kept at it and found it. Well done.", "Correct selection after an incorrect selection or at least 30 seconds of active search."),
            new EncouragementPhrase("persistence", "You stayed with the search and found it. Nice work.", "At least 30 seconds of active search; not a judgment that the person was slow."),
            new EncouragementPhrase("practice", "Nice practice round. You found it.", "This was practice, not a measured study trial."),
            new EncouragementPhrase("quick", "That was quick! You selected the right object.", "Positive active search time at most 10 seconds, no incorrect selections, measured study trial."),
            new EncouragementPhrase("quick_nice", "You found it quickly. Nicely done.", "Positive active search time at most 10 seconds, no incorrect selections, measured study trial."),
            new EncouragementPhrase("quick_complete", "Quick find, correct selection. Well done.", "Positive active search time at most 10 seconds, no incorrect selections, measured study trial.")
        };

        public static string[] AllTexts()
        {
            var texts = new string[Phrases.Length];
            for (int i = 0; i < texts.Length; i++) texts[i] = Phrases[i].text;
            return texts;
        }

        // Claims are qualified locally before the model sees them. Unknown metrics allow only generic praise.
        public static List<EncouragementPhrase> Candidates(RoundPerformance facts, string previous, Func<string, bool> prepared)
        {
            var selected = new List<EncouragementPhrase>();
            bool known = facts != null && facts.measurementsAvailable && facts.incorrectSelections >= 0 &&
                facts.searchSeconds >= 0 && !double.IsNaN(facts.searchSeconds) && !double.IsInfinity(facts.searchSeconds);
            void Add(int index) { var p = Phrases[index]; if (p.text != previous && prepared(p.text)) selected.Add(p); }
            if (known)
            {
                if (facts.practice) Add(9);
                else if (IsQuick(facts)) { Add(10); Add(11); Add(12); }
                else if (facts.incorrectSelections > 0) { Add(6); Add(7); }
                else { Add(4); Add(facts.searchSeconds >= 30 ? 8 : 5); }
            }
            for (int i = 0; i < 4 && selected.Count < 4; i++) Add(i);
            return selected;
        }

        public static string Payload(RoundPerformance facts, IReadOnlyList<EncouragementPhrase> candidates)
        {
            if (candidates == null || candidates.Count < 2 || candidates.Count > 4)
                throw new ArgumentException("Two to four eligible encouragement phrases required.");
            var criteria = new JObject();
            foreach (var p in candidates) criteria.Add(p.text, new JObject { ["id"] = p.id, ["supported_by"] = p.evidence });
            return new JObject {
                ["model"] = "typesafe-ai/jev",
                ["state"] = new JObject { ["schema"] = Version, ["event"] = "round_complete", ["phase"] = "post_selection_feedback",
                    ["outcome"] = "correct_trigger_selection", ["correct_object_selected"] = true,
                    ["selection_method"] = "right_controller_ray_and_trigger", ["search_finished"] = true,
                    ["next_action"] = "Wait for the next round transition; do not give search directions.",
                    ["quick_completion_eligible"] = IsQuick(facts), ["quick_completion_limit_seconds"] = QuickSearchSeconds,
                    ["round"] = facts == null ? (JToken)JValue.CreateNull() : JObject.FromObject(facts),
                    ["measurement_notes"] = "Search seconds exclude pauses. Guidance starts are playback onsets, not proof of listening or reliance. Repeat requests include unavailable repeats. Quick means positive active search time at most 10 seconds with no incorrect selections in a study trial: a pilot wording rule, not a validated speed norm. No comparison baseline or workload inference is available." },
                ["questions"] = new JObject { ["round_encouragement"] = new JObject {
                    ["type"] = "choice", ["criteria"] = criteria,
                    ["instructions"] = "The round is COMPLETE: the user selected the correct object with the right trigger. Choose one short positive voice line from the offered choices. Prefer quick-find feedback when quick_completion_eligible is true, practice acknowledgment for practice, recovery after incorrect selections, persistence for a longer search, otherwise first-selection success. Previous feedback is in round.previousFeedback; avoid repetition. Do not give navigation, ask for another selection, claim the whole study is finished, criticize, infer feelings/workload, compare people or voices, or claim improvement. Speed wording is allowed ONLY in eligible quick phrases. Do not add words or tell the person to rush." }
                }
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        public static EncouragementPhrase Parse(string json, IReadOnlyList<EncouragementPhrase> candidates)
        {
            var answer = JObject.Parse(json)["answers"]?["round_encouragement"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid encouragement answer type.");
            foreach (var p in candidates) if (p.text == (string)answer["choice"]) return p;
            throw new FormatException("Encouragement was outside the eligible phrase set.");
        }
    }
}
