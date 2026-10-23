using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SpatialVoice
{
    /// <summary>One bounded utterance per connection, so late chunks cannot leak into the
    /// next hint/voice. Decode JSON/base64 PCM, not MP3 or arbitrary binary socket frames.</summary>
    public sealed class ElevenLabsPcmClient
    {
        public const int SampleRate = 24000;
        public const string Model = "eleven_turbo_v2_5";
        readonly string m_Key;
        public ElevenLabsPcmClient(string key) { m_Key = key; }

        public static Uri Url(string voiceId)
        {
            if (string.IsNullOrWhiteSpace(voiceId)) throw new ArgumentException("Voice ID required.");
            return new Uri("wss://api.elevenlabs.io/v1/text-to-speech/" + Uri.EscapeDataString(voiceId) +
                "/stream-input?model_id=" + Model + "&output_format=pcm_24000&auto_mode=true");
        }

        public async Task StreamAsync(string text, string voiceId, PcmStreamBuffer output, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(m_Key)) throw new InvalidOperationException("ElevenLabs key is not configured.");
            if (string.IsNullOrWhiteSpace(text) || text.Length > 300) throw new ArgumentException("A short complete phrase is required.");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var socket = new ClientWebSocket())
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                socket.Options.SetRequestHeader("xi-api-key", m_Key);
                await socket.ConnectAsync(Url(voiceId), timeout.Token).ConfigureAwait(false);
                // auto_mode explicitly disables the generation chunk schedule. An empty
                // chunk_length_schedule is not a documented low-latency contract.
                await Send(socket, new JObject { ["text"] = " " }, timeout.Token).ConfigureAwait(false);
                await Send(socket, new JObject { ["text"] = text.Trim() + " ", ["try_trigger_generation"] = true,
                    ["flush"] = true }, timeout.Token).ConfigureAwait(false);
                await Send(socket, new JObject { ["text"] = "" }, timeout.Token).ConfigureAwait(false);
                await ReceiveAsync(socket, output, timeout.Token).ConfigureAwait(false);
                // Dispose closes this single-utterance transport even if the provider
                // sends final JSON without a WebSocket close handshake.
            }
        }
        static Task Send(WebSocket socket, JObject payload, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));
            return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
        }

        /// <summary>Handles fragmented JSON frames and provider final/error messages.</summary>
        public static async Task ReceiveAsync(WebSocket socket, PcmStreamBuffer output, CancellationToken token)
        {
            var chunk = new byte[8192];
            int totalBytes = 0;
            using (var message = new MemoryStream())
            {
                while (true)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(chunk), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) throw new IOException("Speech socket closed before final audio.");
                    if (result.MessageType != WebSocketMessageType.Text) throw new FormatException("Expected JSON speech frame.");
                    if (message.Length + result.Count > 2 * 1024 * 1024) throw new FormatException("Speech frame too large.");
                    message.Write(chunk, 0, result.Count);
                    if (!result.EndOfMessage) continue;
                    var json = JObject.Parse(Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length));
                    message.SetLength(0);
                    if (json["error"] != null || json["message"] != null && json["audio"] == null && json["isFinal"] == null && json["is_final"] == null)
                        throw new IOException("ElevenLabs rejected the speech stream.");
                    string encoded = (string)json["audio"];
                    if (!string.IsNullOrEmpty(encoded))
                    {
                        var pcm = Convert.FromBase64String(encoded);
                        totalBytes += pcm.Length;
                        if (totalBytes > SampleRate * 2 * 30) throw new IOException("Speech exceeded thirty seconds.");
                        output.Write(pcm);
                    }
                    if ((bool?)json["isFinal"] == true || (bool?)json["is_final"] == true)
                    {
                        if (totalBytes == 0) throw new IOException("Speech stream contained no audio.");
                        output.Finish(); return;
                    }
                }
            }
        }
    }
}
