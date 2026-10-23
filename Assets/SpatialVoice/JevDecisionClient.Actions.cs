using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    public sealed partial class JevDecisionClient : INavigationActionDecisionProvider
    {
        public static string ActionPayload(string state, IReadOnlyList<NavigationPhrase> choices)
        {
            if (choices == null || choices.Count < 1 || choices.Count > 4) throw new ArgumentException("One to four prepared actions required.");
            var criteria = new JObject();
            foreach (var p in choices)
            {
                if (p.id != "orient" && p.id != "refine" && p.id != "correct" && p.id != "encourage")
                    throw new ArgumentException("Unsupported action.");
                if (criteria.ContainsKey(p.id)) throw new ArgumentException("Duplicate action.");
                criteria[p.id] = new JObject { ["text"] = p.text,
                    ["semantics"] = JObject.FromObject(p.metadata ?? SpeechPhraseMetadata.Describe(p.text)),
                    ["locally_eligible"] = true,
                    ["reason"] = p.id == "encourage" ? "Recent measured progress in the previously spoken direction; all progress requirements verified locally. No claim of finding the target." :
                        p.id == "correct" ? "Verified target departure/overshoot; current direction leads back toward the target. This is a new event." :
                        "Use this currently verified direction and magnitude to guide the participant toward the hidden target." };
            }
            criteria["wait"] = new JObject { ["text"] = JValue.CreateNull(), ["reason"] = "Remain silent when playback is busy, gaze invalid, the guidance gate is closed, or advice would repeat speech less than 3 seconds ago without a new event." };
            var context = JObject.Parse(state);
            context["eligible_actions"] = criteria.DeepClone();
            context["locally_qualified"] = true;
            return new JObject {
                ["model"] = "typesafe-ai/jev", ["state"] = context,
                ["questions"] = new JObject { ["navigation_action"] = new JObject {
                    ["type"] = "choice",
                    ["instructions"] = "Choose one eligible action or wait. The participant does not know the target. Unity owns geometry, gaze/controller qualification and final playback checks. Use runtime.evidence, runtime.playback.recent_history and runtime.seconds_since_speech_ended. Invalid gaze, busy/playing audio or guidanceState.newGuidanceAllowed=false requires wait. An offered encourage action means recent measured progress and same direction were verified by Unity. Prefer encourage over another orient/refine cue when it is offered and silence exceeds 3 seconds. Prefer a useful cue rather than wait after more than 3 seconds of silence. A verified return after target acknowledgment is a new corrective event: correct or refine may help. Do not confuse gaze confirmation with pointing or completed selection. Do not repeat the same ordinary direction within 3 seconds without a new event. Current evidence overrides history; proposed speech is not spoken history. All offered phrases are prepared; never invent text.",
                    ["criteria"] = criteria } }
            }.ToString(Formatting.None);
        }
        public static GuidanceSelection ParseAction(string json, IReadOnlyList<NavigationPhrase> choices)
        {
            var answer = JObject.Parse(json)["answers"]?["navigation_action"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid action answer type.");
            string id = (string)answer["choice"];
            if (id == "wait") return new GuidanceSelection { Phrase = choices[0], Speak = false, Source = "jev" };
            foreach (var p in choices) if (p.id == id) return new GuidanceSelection { Phrase = p, Speak = true, Source = "jev" };
            throw new FormatException("Action was not in the offered set.");
        }
        public async Task<GuidanceSelection> ChooseActionAsync(string state, IReadOnlyList<NavigationPhrase> choices,
            CancellationToken cancellation)
        {
            var payload = JObject.Parse(ActionPayload(state, choices));
            string id = (string)payload["state"]["decision_id"] ?? Guid.NewGuid().ToString("N");
            payload["state"]["decision_id"] = id;
            var json = await RequestAsync(payload.ToString(Formatting.None), cancellation,
                GuidanceSpeechPolicy.CatalogChoiceDeadlineMilliseconds).ConfigureAwait(false);
            var selected = ParseAction(json, choices); selected.RequestId = id; return selected;
        }
    }
}
