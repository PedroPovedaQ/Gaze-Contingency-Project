using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Verbatim ASR gate. Never supplies the expected phrase as a recognition hint.</summary>
public static class VoiceAudioVerification
{
    public const string Version = "scribe-v2-verbatim-v2";
    public const string Endpoint = "https://api.elevenlabs.io/v1/speech-to-text";
    public sealed class Result { public bool matches; public string transcript, error; }

    public static string Canonical(string text)
    {
        string value = (text ?? "").ToLowerInvariant().Replace('’', '\'');
        string[,] contractions = { {"you're","you are"}, {"you've","you have"}, {"you'll","you will"},
            {"don't","do not"}, {"that's","that is"}, {"let's","let us"}, {"i'm","i am"},
            {"it's","it is"}, {"we're","we are"}, {"we've","we have"}, {"we'll","we will"},
            {"isn't","is not"}, {"doesn't","does not"}, {"can't","cannot"} };
        for (int i = 0; i < contractions.GetLength(0); i++)
            value = Regex.Replace(value, @"\b" + Regex.Escape(contractions[i,0]) + @"\b", contractions[i,1]);
        value = Regex.Replace(value, @"\bnasa\s+t\s+l\s+x\b", "nasa tlx");
        value = Regex.Replace(value, @"\balright\b", "all right");
        value = Regex.Replace(value, @"\bok\b", "okay");
        value = VoiceSynthesisRequest.ExpandDecimals(value);
        value = Regex.Replace(value, @"\b\d{1,3}\b", m => VoiceSynthesisRequest.NumberWords(int.Parse(m.Value)));
        value = Regex.Replace(value, @"\bhundred\s+and\b", "hundred");
        value = Regex.Replace(Regex.Replace(value, @"[^\p{L}\p{N}]+", " ").Trim(), @"\s+", " ");
        // Scribe spells the identical-sounding direction as "write" in diagonal
        // guidance. Limit equivalence to that directional context, preserving words.
        return Regex.Replace(value, @"\b(up|down) and write\b", "$1 and right");
    }
    public static bool Matches(string expected, string transcript) => !string.IsNullOrWhiteSpace(transcript) &&
        !VoiceAudioIntegrity.HasForbiddenFiller(transcript) && !VoiceAudioIntegrity.HasForbiddenFiller(expected) &&
        Canonical(expected) == Canonical(transcript);

    public static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    public static bool HasReceipt(string path, string expected)
    {
        try
        {
            if (!File.Exists(path) || !File.Exists(path + ".verified.json")) return false;
            var receipt = JObject.Parse(File.ReadAllText(path + ".verified.json"));
            return (string)receipt["version"] == Version && (string)receipt["expected"] == expected &&
                Matches(expected, (string)receipt["transcript"]) &&
                (string)receipt["sha256"] == Hash(File.ReadAllBytes(path));
        }
        catch (Exception) { return false; }
    }
    public static void SaveReceipt(string path, string expected, byte[] bytes, string transcript)
    {
        if (!Matches(expected, transcript) || VoiceAudioIntegrity.IsRejectedAudio(bytes))
            throw new InvalidOperationException("Audio did not pass transcript verification.");
        File.WriteAllText(path + ".verified.json", new JObject { ["version"] = Version,
            ["expected"] = expected, ["transcript"] = transcript, ["sha256"] = Hash(bytes),
            ["verified_at_utc"] = DateTime.UtcNow.ToString("O") }.ToString());
    }

    public static IEnumerator Verify(string apiKey, byte[] audio, string expected, Action<Result> done)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) { done(new Result { error = "Audio verification requires an ElevenLabs key." }); yield break; }
        var form = new WWWForm();
        form.AddField("model_id", "scribe_v2");
        form.AddField("language_code", "en");
        form.AddField("no_verbatim", "false"); // True would conceal the very fillers we must reject.
        form.AddField("tag_audio_events", "true");
        form.AddField("diarize", "false");
        form.AddField("temperature", "0");
        form.AddBinaryData("file", audio, "generated-line.mp3", "audio/mpeg");
        using (var request = UnityWebRequest.Post(Endpoint, form))
        {
            request.SetRequestHeader("xi-api-key", apiKey);
            request.timeout = 30;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            { done(new Result { error = "Audio verification failed: HTTP " + request.responseCode }); yield break; }
            Result result;
            try
            {
                var transcript = (string)JObject.Parse(request.downloadHandler.text)["text"];
                result = new Result { transcript = transcript, matches = Matches(expected, transcript) };
            }
            catch (Exception) { result = new Result { error = "Invalid audio verification response." }; }
            done(result);
        }
    }
}
