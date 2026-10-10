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
                criteria[p.text] = "Tone: " + p.tone + ". Geometrically valid; choose for conversational fit.";
            }
            return new JObject {
                ["model"] = "typesafe-ai/jev", ["state"] = state,
                ["questions"] = new JObject { ["navigation_phrase"] = new JObject {
                    ["type"] = "choice", ["instructions"] = "Choose one short navigation phrase. Prefer the requested tone, avoid repeating the last phrase. Do not infer task success or target passage.",
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
                ["model"] = "typesafe-ai/jev", ["state"] = state,
                ["questions"] = new JObject { ["speech_action"] = new JObject {
                    ["type"] = "choice",
                    ["instructions"] = "Choose finish_phrase by default. Choose fade_out only when the current spoken instruction is now misleading or contradicts a stable new target/area confirmation, and enough audio remains for interrupting to help. Minor refinements, repeated directions and conversational variation should finish. This is semantic advice, not word-boundary detection.",
                    ["criteria"] = new JObject {
                        ["finish_phrase"] = "Preserve the current sentence; reassess after it finishes.",
                        ["fade_out"] = "Fade the now-misleading instruction for 150 milliseconds, stop, then revalidate the proposed correction." }
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
