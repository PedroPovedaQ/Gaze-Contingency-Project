using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    public sealed class GuidanceSelection
    {
        public NavigationPhrase Phrase;
        public bool Speak;
        public string Source;
        public string RequestId;
    }

    /// <summary>Jev chooses wording, never geometry. Uses Vercel's TypeSafe-compatible contract,
    /// not the unverified api.jev.ai endpoint. No credentials or provider bodies in errors.</summary>
    public sealed class JevDecisionClient : INavigationDecisionProvider
    {
        public const string Endpoint = "https://ai-gateway.vercel.sh/typesafe/v1/systemone";
        static readonly HttpClient Client = new HttpClient();
        readonly string m_Key;
        readonly SynchronizationContext m_Context = SynchronizationContext.Current;
        // Replay telemetry only; never emit headers, keys or arbitrary provider error bodies.
        public event Action<string> Trace;
        void Emit(JObject value)
        {
            string json = value.ToString(Formatting.None);
            void Deliver() { try { Trace?.Invoke(json); } catch { /* Diagnostics cannot break guidance. */ } }
            if (m_Context != null && SynchronizationContext.Current != m_Context) m_Context.Post(_ => Deliver(), null);
            else Deliver();
        }
        readonly HttpClient m_Client;
        public string Name => "jev";
        public bool Available => !string.IsNullOrWhiteSpace(m_Key);
        public JevDecisionClient(string apiKey, HttpClient client = null) { m_Key = apiKey; m_Client = client ?? Client; }

        public static string Payload(string state, IReadOnlyList<NavigationPhrase> choices)
        {
            if (choices == null || choices.Count < 2 || choices.Count > 5) throw new ArgumentException("Two to five candidates required.");
            var criteria = new JObject();
            foreach (var p in choices)
            {
                if (criteria.ContainsKey(p.text)) throw new ArgumentException("Candidate texts must be unique.");
                criteria[p.text] = new JObject { ["tone"] = p.tone.ToString(), ["word_count"] = p.text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length,
                    ["semantics"] = JObject.FromObject(p.metadata ?? SpeechPhraseMetadata.Describe(p.text)),
                    ["geometrically_valid"] = true };
            }
            return new JObject {
                ["model"] = "typesafe-ai/jev", ["state"] = state,
                ["questions"] = new JObject { ["navigation_phrase"] = new JObject {
                    ["type"] = "choice", ["instructions"] = "Choose one short navigation phrase. Prefer the requested tone and never repeat the last phrase when another valid choice exists. For Casual guidance, use a short Direct cue as the next alternative; avoid Technical wording unless Technical is requested. Do not infer task success or target passage.",
                    ["criteria"] = criteria } }
            }.ToString(Formatting.None);
        }

        public static NavigationPhrase Parse(string json, IReadOnlyList<NavigationPhrase> candidates)
        {
            var answer = JObject.Parse(json)["answers"]?["navigation_phrase"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid Jev answer type.");
            var choice = (string)answer?["choice"];
            foreach (var p in candidates) if (string.Equals(p.text, choice, StringComparison.Ordinal)) return p;
            throw new FormatException("Jev choice is outside the candidate set.");
        }

        public async Task<NavigationPhrase> ChooseAsync(string state, IReadOnlyList<NavigationPhrase> candidates,
            CancellationToken cancellation, int timeoutMilliseconds = GuidanceSpeechPolicy.CatalogChoiceDeadlineMilliseconds)
        {
            return Parse(await RequestAsync(Payload(state, candidates), cancellation, timeoutMilliseconds).ConfigureAwait(false), candidates);
        }

        // Advisory only: the Unity caller still owns validity, identity, playback and timing gates.
        public async Task<bool> ShouldInterruptAsync(string state, CancellationToken cancellation)
        {
            var payload = new JObject {
                ["model"] = "typesafe-ai/jev", ["state"] = JObject.Parse(state),
                ["questions"] = new JObject { ["speech_action"] = new JObject {
                    ["type"] = "choice",
                    ["instructions"] = "Does the new verified event make the CURRENT instruction misleading? Use authored semantics and evidence, not the word right in right area. A proposed target_confirmation means stop searching, not a completed trigger selection. Do not infer word boundaries.",
                    ["criteria"] = new JObject {
                        ["finish_phrase"] = "The current direction remains correct, is merely refined, or either phrase has unknown semantics. An arrival_already_acknowledged_by_current_stage=true cannot interrupt: this is the SAME wall. Also finish if evidence is invalid, current interruptPolicy is finish, playback is not tip/guidance_repeat, elapsed < 0.65s or remaining < 1s. Progress and same-direction rewording always wait.",
                        ["fade_out"] = "Valid stable event supersedes an interruptible search instruction with enough speech left: target acquired/pointed at, a confirmed reversal after passing/leaving the target, or target wall reached while still turning toward it. Current turn/search/return instruction would now move the listener away. Not for same-direction refinement or added diagonal." }
                } }
            }.ToString(Formatting.None);
            return ParseInterruption(await RequestAsync(payload, cancellation, 400).ConfigureAwait(false));
        }

        static JObject TransmissionQuestion() => new JObject {
                    ["type"] = "choice",
                    ["instructions"] = "Choose speak or wait for an idle navigation assistant. The participant does not know the target. Use seconds_since_speech_ended as elapsed silence, not time since a request. pending_phrase is this proposed cue, not playing audio. A fresh correction after several seconds of silence is useful, even if older advice gave the opposite direction. A 0.6-second cadence is an opportunity, not an obligation. This decision cannot interrupt audio.",
                    ["criteria"] = new JObject {
                        ["speak"] = "Valid gaze, idle audio, and an actionable proposed cue. Speak for a new direction or qualified event; also speak when silence exceeds 3 seconds and the user still needs guidance.",
                        ["wait"] = "Audio actually playing; invalid gaze; or redundant advice spoken within the past 3 seconds with no new direction/event. Also wait for target confirmations without local qualification or advice that contradicts current evidence." }
                };

        public async Task<bool> ShouldTransmitAsync(string state, CancellationToken cancellation)
        {
            var payload = new JObject {
                ["model"] = "typesafe-ai/jev", ["state"] = JObject.Parse(state),
                ["questions"] = new JObject { ["transmit"] = TransmissionQuestion() }
            }.ToString(Formatting.None);
            return ParseTransmission(await RequestAsync(payload, cancellation, 400).ConfigureAwait(false));
        }

        // Coarse guidance used to make these two requests in series. Both typed answers
        // now share one immutable state and one deadline; neither can override Unity gates.
        public static string GuidancePayload(string state, IReadOnlyList<NavigationPhrase> candidates)
        {
            var payload = JObject.Parse(Payload(state, candidates));
            payload["state"] = JObject.Parse(state);
            var question = TransmissionQuestion();
            question["instructions"] = "Decide whether to speak a navigation cue NOW. First check state.runtime.evidence.valid and state.runtime.playback.is_speaking: invalid gaze or currently speaking MUST answer wait. Otherwise consider elapsed silence at state.runtime.seconds_since_speech_ended and the last spoken phrase. Candidates are alternatives for the SAME locally verified direction, not permission to speak. Select navigation_phrase even when waiting. Never interrupt audio.";
            question["criteria"] = new JObject {
                ["speak"] = "Gaze valid, playback idle, locally qualified direction; AND either a new direction/event or more than 3 seconds of silence. All preconditions must hold.",
                ["wait"] = "Gaze invalid, playback speaking/busy, or no locally qualified cue. Also wait for same-direction advice less than 3 seconds after the last spoken instruction. Any failed precondition requires wait regardless of target angle." };

            payload["questions"]["transmit"] = question;
            return payload.ToString(Formatting.None);
        }

        public async Task<GuidanceSelection> ChooseGuidanceAsync(string state, IReadOnlyList<NavigationPhrase> candidates,
            CancellationToken cancellation)
        {
            var payload = JObject.Parse(GuidancePayload(state, candidates));
            string id = (string)payload["state"]?["decision_id"] ?? Guid.NewGuid().ToString("N");
            payload["state"]["decision_id"] = id;
            string json = await RequestAsync(payload.ToString(Formatting.None), cancellation,
                GuidanceSpeechPolicy.CatalogChoiceDeadlineMilliseconds).ConfigureAwait(false);
            var selection = ParseGuidance(json, candidates);
            selection.RequestId = id;
            return selection;
        }

        public static GuidanceSelection ParseGuidance(string json, IReadOnlyList<NavigationPhrase> candidates) =>
            new GuidanceSelection { Phrase = Parse(json, candidates), Speak = ParseTransmission(json), Source = "jev" };

        // One synthetic request after voice preparation primes the shared HTTP connection
        // before a time-critical hint. It cannot authorize playback or delay the trial.
        public async Task WarmAsync(CancellationToken cancellation)
        {
            var payload = new JObject { ["model"] = "typesafe-ai/jev",
                ["state"] = new JObject { ["task"] = "synthetic_connection_preparation", ["participant_data"] = false },
                ["questions"] = new JObject { ["ready"] = new JObject { ["type"] = "choice",
                    ["instructions"] = "This is a synthetic connection check, not navigation. Choose ready.",
                    ["criteria"] = new JObject { ["ready"] = "Connection preparation only.", ["wait"] = "Unused alternative." } } }
            }.ToString(Formatting.None);
            await RequestAsync(payload, cancellation, 1500).ConfigureAwait(false);
        }

        public static bool ParseTransmission(string json)
        {
            var answer = JObject.Parse(json)["answers"]?["transmit"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid transmission answer type.");
            string choice = (string)answer?["choice"];
            if (choice == "speak") return true;
            if (choice == "wait") return false;
            throw new FormatException("Transmission choice is outside the candidate set.");
        }

        public static bool ParseInterruption(string json)
        {
            var answer = JObject.Parse(json)["answers"]?["speech_action"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid interruption answer type.");
            string choice = (string)answer?["choice"];
            if (choice == "fade_out") return true;
            if (choice == "finish_phrase") return false;
            throw new FormatException("Interruption choice is outside the candidate set.");
        }

        public async Task<EncouragementPhrase> ChooseEncouragementAsync(RoundPerformance facts,
            IReadOnlyList<EncouragementPhrase> candidates, CancellationToken cancellation, string decisionId = null)
        {
            var payload = JObject.Parse(RoundEncouragement.Payload(facts, candidates));
            payload["state"]["decision_id"] = decisionId ?? Guid.NewGuid().ToString("N");
            return RoundEncouragement.Parse(await RequestAsync(payload.ToString(Formatting.None), cancellation,
                RoundEncouragement.DeadlineMilliseconds).ConfigureAwait(false), candidates);
        }

        async Task<string> RequestAsync(string payload, CancellationToken cancellation, int timeoutMilliseconds)
        {
            var request = JObject.Parse(payload);
            var state = request["state"];
            string id = state is JObject ? (string)state["decision_id"] : null;
            id = id ?? Guid.NewGuid().ToString("N");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Emit(new JObject { ["schema"] = "jev-trace-v1", ["request_id"] = id,
                ["stage"] = "request", ["deadline_ms"] = timeoutMilliseconds, ["payload"] = request });
            try
            {
                string json = await SendAsync(payload, cancellation, timeoutMilliseconds).ConfigureAwait(false);
                var response = JObject.Parse(json);
                // Validate before labeling this an accepted provider response.
                foreach (var question in ((JObject)request["questions"]).Properties())
                {
                    var answer = response["answers"]?[question.Name];
                    string choice = (string)answer?["choice"];
                    if ((string)answer?["type"] != "choice" || choice == null || question.Value["criteria"]?[choice] == null)
                        throw new FormatException("Invalid Jev choice response.");
                }
                Emit(new JObject { ["schema"] = "jev-trace-v1", ["request_id"] = id,
                    ["stage"] = "response", ["status"] = "ok", ["elapsed_ms"] = timer.Elapsed.TotalMilliseconds,
                    ["answers"] = response["answers"], ["usage"] = response["usage"] });
                return json;
            }
            catch (Exception e)
            {
                Emit(new JObject { ["schema"] = "jev-trace-v1", ["request_id"] = id, ["stage"] = "response",
                    ["status"] = cancellation.IsCancellationRequested ? "cancelled" : e is OperationCanceledException ? "timeout" : "error",
                    ["error_type"] = e.GetType().Name, ["elapsed_ms"] = timer.Elapsed.TotalMilliseconds });
                throw;
            }
        }

        async Task<string> SendAsync(string payload, CancellationToken cancellation, int timeoutMilliseconds)
        {
            if (!Available) throw new InvalidOperationException("Vercel Jev key is not configured.");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                timeout.CancelAfter(timeoutMilliseconds);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", m_Key);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using (var response = await m_Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw new HttpRequestException("TypeSafe HTTP " + (int)response.StatusCode);
                    // Bound the response and honor cancellation while reading the body too.
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var bytes = new System.IO.MemoryStream())
                    {
                        var buffer = new byte[4096];
                        int n;
                        while ((n = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                        {
                            if (bytes.Length + n > 65536) throw new FormatException("TypeSafe response too large.");
                            bytes.Write(buffer, 0, n);
                        }
                        return Encoding.UTF8.GetString(bytes.ToArray());
                    }
                }
            }
        }
    }
}
