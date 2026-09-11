using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Records a short microphone sample and clones it via <see cref="VoxtralClient"/>,
/// storing the resulting voice id in <see cref="SessionConfig.SelfSimilarVoiceId"/>.
/// Drives the self-similar launch mode's enrollment step.
/// </summary>
public class VoiceEnrollment : MonoBehaviour
{
    const string k_Tag = "[Enroll]";
    const int k_SampleRate = 16000;   // mono 16 kHz keeps the base64 sample small
    const int k_DefaultSeconds = 40;

    VoxtralClient m_Voxtral;
    AudioClip m_RecordingClip;
    bool m_Busy;
    bool m_FinishRequested;
    public bool CanFinishRecording => Current == State.Recording && !m_FinishRequested &&
        m_RecordingClip != null && Microphone.GetPosition(null) >= k_SampleRate;

    public void FinishRecording()
    {
        if (CanFinishRecording) m_FinishRequested = true;
    }

    public enum State { Idle, Recording, Cloning, Done, Failed }
    public State Current { get; private set; } = State.Idle;
    public string LastError { get; private set; } = "";
    public bool IsBusy => m_Busy;

    public void Initialize(VoxtralClient voxtral)
    {
        m_Voxtral = voxtral != null ? voxtral : GetComponent<VoxtralClient>();
        if (m_Voxtral == null)
            Debug.LogWarning($"{k_Tag} No VoxtralClient supplied — enrollment will fail.");
    }

    /// <summary>
    /// Records <paramref name="seconds"/> of audio, clones it, and sets
    /// SessionConfig.SelfSimilarVoiceId. Reports progress via optional callbacks.
    /// </summary>
    public void RecordAndClone(int seconds = k_DefaultSeconds,
        Action<State> onState = null, Action<string> onDone = null, Action<string> onError = null,
        Func<IEnumerator> beforeRecording = null)
    {
        if (m_Busy) { onError?.Invoke("enrollment already running"); return; }
        StartCoroutine(RecordAndCloneCoroutine(seconds, onState, onDone, onError, beforeRecording));
    }

    IEnumerator RecordAndCloneCoroutine(int seconds,
        Action<State> onState, Action<string> onDone, Action<string> onError, Func<IEnumerator> beforeRecording)
    {
        m_Busy = true;
        m_FinishRequested = false;
        LastError = "";
        void Set(State s) { Current = s; onState?.Invoke(s); }

        if (m_Voxtral == null || !m_Voxtral.HasKey)
        {
            Fail("voice provider unavailable", onError); Set(State.Failed); m_Busy = false; yield break;
        }

        // Mic permission (Android/Quest/Vive).
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            float waited = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && waited < 20f)
            { waited += Time.deltaTime; yield return null; }
        }
#endif
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Fail("no microphone device", onError); Set(State.Failed); m_Busy = false; yield break;
        }

        // Finish the countdown/tone after permission checks and before opening the mic.
        if (beforeRecording != null) yield return beforeRecording();
        seconds = Mathf.Clamp(seconds, 10, 60);
        Debug.Log($"{k_Tag} Recording {seconds}s...");
        // Spare capacity keeps the cursor valid when the automatic deadline is reached.
        m_RecordingClip = Microphone.Start(null, false, seconds + 1, k_SampleRate);
        if (m_RecordingClip == null)
        {
            Fail("Microphone.Start returned null", onError); Set(State.Failed); m_Busy = false; yield break;
        }

        Set(State.Recording);
        float started = Time.realtimeSinceStartup;
        while (!m_FinishRequested && Time.realtimeSinceStartup - started < seconds && Microphone.IsRecording(null))
            yield return null;

        int recordedSamples = Microphone.GetPosition(null);
        Microphone.End(null);

        if (recordedSamples < k_SampleRate)
        {
            Destroy(m_RecordingClip); m_RecordingClip = null;
            Fail("recording too short or microphone interrupted", onError); Set(State.Failed); m_Busy = false; yield break;
        }

        byte[] wav = WavUtility.EncodeFromClip(m_RecordingClip, recordedSamples);
        Destroy(m_RecordingClip); m_RecordingClip = null;
        Debug.Log($"{k_Tag} Encoded WAV: {wav.Length} bytes ({recordedSamples} samples)");

        Set(State.Cloning);
        string voiceId = null; string err = null;
        yield return m_Voxtral.CloneVoice(wav, $"study_{SessionConfig.ParticipantId}",
            id => voiceId = id, e => err = e);

        if (!string.IsNullOrEmpty(voiceId))
        {
            SessionConfig.SelfSimilarVoiceId = voiceId;
            Set(State.Done);
            Debug.Log($"{k_Tag} Enrollment complete. voiceId={voiceId}");
            onDone?.Invoke(voiceId);
        }
        else
        {
            Fail(err ?? "clone failed", onError); Set(State.Failed);
        }
        m_Busy = false;
    }

    void Fail(string msg, Action<string> onError)
    {
        LastError = msg;
        Debug.LogWarning($"{k_Tag} {msg}");
        onError?.Invoke(msg);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (m_RecordingClip != null)
        {
            Microphone.End(null);
            Destroy(m_RecordingClip); m_RecordingClip = null;
        }
        m_Busy = false;
        Current = State.Idle;
    }
}
