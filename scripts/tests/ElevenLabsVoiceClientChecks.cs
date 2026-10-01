// Run the production HTTP client against deterministic transport responses, without uploads.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
class ElevenLabsVoiceClientChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Drain(IEnumerator task) { while (task.MoveNext()) if (task.Current is IEnumerator child) Drain(child); }
    static void Main()
    {
        var client = new ElevenLabsVoiceClient();
        string id = null, error = null;
        Action clone = () => { id = error = null; Drain(client.CloneVoice(new byte[32044], "study_TEST", x => id = x, x => error = x)); };
        clone(); Check(id == null && error.Contains("key") && UnityWebRequest.Last == null, "No key blocks upload");
        client.Initialize("test-key");
        Drain(client.CloneVoice(new byte[44], "test", x => id = x, x => error = x));
        Check(UnityWebRequest.Last == null, "Short audio never uploaded");
        UnityWebRequest.ResponseText = "{\"voice_id\":\"new-clone\",\"requires_verification\":false}";
        clone();
        var request = UnityWebRequest.Last;
        Check(id == "new-clone" && error == null, "Reads ElevenLabs voice_id response");
        Check(request.Url == "https://api.elevenlabs.io/v1/voices/add" && request.Method == "POST", "Clone endpoint");
        Check(request.Form.Fields["name"] == "study_TEST" && request.Form.Fields["remove_background_noise"] == "false", "Enrollment form metadata");
        Check(request.Form.FileField == "files" && request.Form.FileType == "audio/wav" && request.Form.Bytes.Length == 32044, "Multipart WAV contract");
        Check(request.Headers["xi-api-key"] == "test-key" && !request.Headers.ContainsKey("Authorization"), "ElevenLabs authentication");
        foreach (var response in new[] { "{\"voice_id\":\"locked\",\"requires_verification\":true}", "{}", "not json" })
        {
            UnityWebRequest.ResponseText = response; clone();
            Check(id == null && !string.IsNullOrEmpty(error), "Unverified or malformed clone must not become session voice");
        }
        UnityWebRequest.Status = 403; UnityWebRequest.ResponseText = "private provider response";
        clone(); Check(id == null && error.Contains("403") && !error.Contains("private provider"), "Safe access error");
        UnityWebRequest.Status = 200; UnityWebRequest.ContentType = "audio/mpeg"; UnityWebRequest.Bytes = new byte[120];
        byte[] audio = null;
        Action speak = () => { audio = null; error = null; Drain(client.Synthesize("I need to look left.", "new-clone", x => audio = x, x => error = x)); };
        speak(); Check(audio != null && error == null, "MP3 response accepted");
        request = UnityWebRequest.Last;
        Check(request.Url == "https://api.elevenlabs.io/v1/text-to-speech/new-clone", "Clone speech endpoint");
        var body = System.Text.Encoding.UTF8.GetString(request.uploadHandler.Bytes);
        Check(body.Contains("eleven_turbo_v2_5") && body.Contains("I need to look left."), "Same model and unchanged formatted speech");
        UnityWebRequest.ContentType = "application/json"; speak();
        Check(audio == null && error.Contains("non-audio"), "HTTP 200 JSON never cached as audio");
        UnityWebRequest.ContentType = "audio/mpeg"; UnityWebRequest.Bytes = new byte[2]; speak();
        Check(audio == null && error.Contains("empty"), "Empty audio rejected");
        UnityWebRequest.Status = 429; speak();
        Check(audio == null && error.Contains("quota"), "Quota failure is actionable");
        Console.WriteLine("PASS: ElevenLabs multipart/auth, clone ID, verification/malformed/access failures, speech model, and non-audio/empty/quota rejection.");
    }
}
namespace UnityEngine
{
    public class MonoBehaviour { }
    public class WWWForm
    {
        public Dictionary<string,string> Fields = new Dictionary<string,string>();
        public string FileField, FileType; public byte[] Bytes;
        public void AddField(string name,string value) => Fields[name] = value;
        public void AddBinaryData(string name,byte[] bytes,string filename,string type) { FileField = name; Bytes = bytes; FileType = type; }
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string value) => new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<T>(value);
        public static string ToJson(object value) => new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(value);
    }
}
namespace UnityEngine.Networking
{
    public class UploadHandlerRaw { public byte[] Bytes; public UploadHandlerRaw(byte[] bytes) { Bytes = bytes; } }
    public class DownloadHandlerBuffer { public string text => UnityWebRequest.ResponseText; public byte[] data => UnityWebRequest.Bytes; }
    public class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ProtocolError }
        public static UnityWebRequest Last;
        public static string ResponseText = "", ContentType = "application/json";
        public static byte[] Bytes;
        public static long Status = 200;
        public long responseCode => Status;
        public Result result => Status == 200 ? Result.Success : Result.ProtocolError;
        public int timeout;
        public string Url, Method; public WWWForm Form;
        public Dictionary<string,string> Headers = new Dictionary<string,string>();
        public UploadHandlerRaw uploadHandler; public DownloadHandlerBuffer downloadHandler = new DownloadHandlerBuffer();
        public UnityWebRequest(string url,string method) { Url = url; Method = method; Last = this; }
        public static UnityWebRequest Post(string url,WWWForm form) => new UnityWebRequest(url,"POST") { Form = form };
        public void SetRequestHeader(string name,string value) => Headers[name] = value;
        public string GetResponseHeader(string name) => ContentType;
        public IEnumerator SendWebRequest() { yield return null; }
        public void Dispose() { }
    }
}
