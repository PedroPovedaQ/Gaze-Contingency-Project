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
    /// <summary>Jev chooses wording, never geometry. Uses Vercel's TypeSafe-compatible contract,
    /// not the unverified api.jev.ai endpoint. No credentials or provider bodies in errors.</summary>
    public sealed class JevDecisionClient
    {
        public const string Endpoint = "https://ai-gateway.vercel.sh/typesafe/v1/systemone";
        static readonly HttpClient Client = new HttpClient();
        readonly string m_Key;
        readonly HttpClient m_Client;
        public bool Available => !string.IsNullOrWhiteSpace(m_Key);
        public JevDecisionClient(string apiKey, HttpClient client = null) { m_Key = apiKey; m_Client = client ?? Client; }

        public static string Payload(string state, IReadOnlyList<NavigationPhrase> choices)
        {
            if (choices == null || choices.Count < 2 || choices.Count > 4) throw new ArgumentException("Two to four candidates required.");
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
            CancellationToken cancellation, int timeoutMilliseconds = 900)
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

        public static bool ParseInterruption(string json)
        {
            var answer = JObject.Parse(json)["answers"]?["speech_action"];
            if ((string)answer?["type"] != "choice") throw new FormatException("Invalid interruption answer type.");
            string choice = (string)answer?["choice"];
            if (choice == "fade_out") return true;
            if (choice == "finish_phrase") return false;
            throw new FormatException("Interruption choice is outside the candidate set.");
        }

        async Task<string> RequestAsync(string payload, CancellationToken cancellation, int timeoutMilliseconds)
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
