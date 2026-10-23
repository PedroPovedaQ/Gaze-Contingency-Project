using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public partial class VoiceSynthesizer
{
    VoiceDownloadBatch m_DownloadBatch;
    int m_PreparationGeneration;
    Func<string, byte[], string, Action<VoiceAudioVerification.Result>, IEnumerator> m_VerifyAudio = VoiceAudioVerification.Verify;

    sealed class DownloadJob
    {
        public string text, voiceId, path, error, transcript;
        public bool clone;
    }

    // Capture identity, formatted wording and path before any request starts. Downloads never
    // change SessionConfig.Voice or touch the playback/audit/normalization fields.
    List<DownloadJob> DownloadJobs(IEnumerable<KeyValuePair<VoiceCondition, string>> lines)
    {
        var jobs = new List<DownloadJob>();
        var paths = new HashSet<string>();
        foreach (var line in lines)
        {
            bool clone = line.Key == VoiceCondition.SelfSimilar;
            string id = clone ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
            string text = VoicePromptText.FormatForVoice(line.Value, line.Key);
            string path = GetCachePath(text, clone ? $"el-clone-{id}" : $"el-{id}");
            if (!paths.Add(path)) continue;
            jobs.Add(new DownloadJob { text = text, voiceId = id, path = path, clone = clone,
                error = VoiceAudioIntegrity.HasForbiddenFiller(text) ? "Script contains a prohibited filler." :
                    clone && (SessionConfig.SelfSimilarEnrollmentPending || string.IsNullOrEmpty(id))
                    ? "Self-similar voice unavailable." : string.IsNullOrEmpty(id) ? "Neutral voice unavailable." : null });
        }
        return jobs;
    }

    IEnumerator DownloadLines(List<DownloadJob> jobs, Action<int> progress = null)
    {
        var workers = new List<IEnumerator>();
        foreach (var job in jobs) workers.Add(DownloadLine(job));
        var batch = new VoiceDownloadBatch(workers);
        m_DownloadBatch = batch;
        var run = batch.Run();
        try
        {
            while (run.MoveNext())
            {
                progress?.Invoke(batch.Completed);
                yield return run.Current;
            }
            if (batch.Error != null && jobs.Count > 0) jobs[0].error = "Voice download failed: " + batch.Error.GetType().Name;
            progress?.Invoke(batch.Completed);
        }
        finally
        {
            (run as IDisposable)?.Dispose();
            batch.Dispose();
            if (ReferenceEquals(m_DownloadBatch, batch)) m_DownloadBatch = null;
        }
    }

    IEnumerator DownloadLine(DownloadJob job)
    {
        int generation = m_PreparationGeneration;
        if (job.error != null) yield break;
        if (VoiceAudioIntegrity.UsableCache(job.path))
        {
            if (VoiceAudioVerification.HasReceipt(job.path, job.text)) yield break;
            // Legacy caches must earn a receipt too; their existing PCM/RMS checks did not prove wording.
            byte[] cached = File.ReadAllBytes(job.path);
            VoiceAudioVerification.Result checkedCache = null;
            yield return m_VerifyAudio(m_ApiKey, cached, job.text, r => checkedCache = r);
            if (generation != m_PreparationGeneration) yield break;
            if (checkedCache == null || checkedCache.error != null)
            { job.error = checkedCache?.error ?? "Audio verification did not complete."; yield break; }
            if (checkedCache.matches)
            { VoiceAudioVerification.SaveReceipt(job.path, job.text, cached, checkedCache.transcript); yield break; }
            File.Move(job.path, job.path + ".rejected-" + Guid.NewGuid().ToString("N"));
            AudioTelemetry("audio_verification_rejected", "verification", Path.GetFileNameWithoutExtension(job.path),
                "cached_transcript_mismatch");
        }
        for (int attempt = 0; attempt < 3; attempt++)
        {
            byte[] audio = null;
            job.error = null;
            if (job.clone)
            {
                if (m_CloneClient == null || !m_CloneClient.HasKey) { job.error = "Self-similar provider unavailable."; yield break; }
                yield return m_CloneClient.Synthesize(job.text, job.voiceId, b => audio = b, e => job.error = e, attempt);
                if (generation != m_PreparationGeneration) yield break;
            }
            else
            {
                if (string.IsNullOrEmpty(m_ApiKey)) { job.error = "Neutral voice provider unavailable."; yield break; }
                using (var request = new UnityWebRequest(k_BaseUrl + Uri.EscapeDataString(job.voiceId), "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(VoiceSynthesisRequest.Json(job.text, k_Model, attempt)));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Accept", "audio/mpeg");
                    request.SetRequestHeader("xi-api-key", m_ApiKey);
                    request.timeout = 30;
                    yield return request.SendWebRequest();
                    if (generation != m_PreparationGeneration) yield break;
                    if (request.result != UnityWebRequest.Result.Success) job.error = "Neutral synthesis failed: HTTP " + request.responseCode;
                    else if (!(request.GetResponseHeader("Content-Type") ?? "").StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
                        job.error = "Neutral synthesis returned non-audio data.";
                    else audio = request.downloadHandler.data;
                }
            }
            if (job.error == null && (audio == null || audio.Length < 100)) job.error = "Synthesis returned empty or truncated audio.";
            if (job.error == null && VoiceAudioIntegrity.IsRejectedAudio(audio)) job.error = "Synthesis returned a quarantined audio clip.";
            if (job.error == null)
            {
                VoiceAudioVerification.Result verification = null;
                yield return m_VerifyAudio(m_ApiKey, audio, job.text, r => verification = r);
                if (generation != m_PreparationGeneration) yield break;
                if (verification == null || verification.error != null)
                { job.error = verification?.error ?? "Audio verification did not complete."; yield break; }
                if (!verification.matches)
                {
                    job.error = "Audio wording mismatch for \"" + job.text + "\" after " + (attempt + 1) + " attempts.";
                    AudioTelemetry("audio_verification_rejected", "verification", Path.GetFileNameWithoutExtension(job.path),
                        "generated_transcript_mismatch;attempt=" + (attempt + 1));
                    Debug.LogWarning(k_Tag + " Audio verification rejected: " + new Newtonsoft.Json.Linq.JObject {
                        ["phrase"] = job.text, ["transcript"] = Truncate(verification.transcript ?? "", 240),
                        ["attempt"] = attempt + 1, ["clip"] = Path.GetFileNameWithoutExtension(job.path) }
                        .ToString(Newtonsoft.Json.Formatting.None));
                    continue; // Regenerate, never publish or play a mismatched clip.
                }
                // Store the receipt only after the complete MP3 has been committed below.
                job.transcript = verification.transcript;
            }
            if (job.error == null)
            {
                // Complete files only: cancellation cannot expose a partial MP3 to foreground playback.
                string temp = job.path + "." + Guid.NewGuid().ToString("N") + ".download";
                try
                {
                    File.WriteAllBytes(temp, audio);
                    if (File.Exists(job.path)) File.Delete(job.path);
                    File.Move(temp, job.path);
                    VoiceAudioVerification.SaveReceipt(job.path, job.text, audio, job.transcript);
                    AudioTelemetry("audio_verification_passed", "verification", Path.GetFileNameWithoutExtension(job.path), VoiceAudioVerification.Version);
                }
                catch (Exception e) { job.error = "Could not save audio: " + e.GetType().Name; }
                finally { if (File.Exists(temp)) File.Delete(temp); }
                yield break;
            }
            // Do not retry authentication, permission or policy failures. Retry transient errors only.
            bool retry = job.error.Contains("HTTP 429") || job.error.Contains("HTTP 5") || job.error.Contains("HTTP 0");
            if (!retry || attempt == 2) yield break;
            double resume = Time.realtimeSinceStartupAsDouble + (1 << attempt);
            while (Time.realtimeSinceStartupAsDouble < resume) yield return null;
            if (generation != m_PreparationGeneration) yield break;
        }
    }
}
