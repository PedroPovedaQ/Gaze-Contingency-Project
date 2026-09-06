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
        Action<State> onState = null, Action<string> onDone = null, Action<string> onError = null)
    {
        if (m_Busy) { onError?.Invoke("enrollment already running"); return; }
        StartCoroutine(RecordAndCloneCoroutine(seconds, onState, onDone, onError));
    }

    IEnumerator RecordAndCloneCoroutine(int seconds,
        Action<State> onState, Action<string> onDone, Action<string> onError)
    {
        m_Busy = true;
        void Set(State s) { Current = s; onState?.Invoke(s); }

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

        Set(State.Recording);
        Debug.Log($"{k_Tag} Recording {seconds}s...");
        m_RecordingClip = Microphone.Start(null, false, Mathf.Clamp(seconds, 10, 60), k_SampleRate);
        if (m_RecordingClip == null)
        {
            Fail("Microphone.Start returned null", onError); Set(State.Failed); m_Busy = false; yield break;
        }

        float t = 0f;
        while (t < seconds && Microphone.IsRecording(null))
        { t += Time.deltaTime; yield return null; }

        int recordedSamples = Microphone.GetPosition(null);
        Microphone.End(null);

        byte[] wav = WavUtility.EncodeFromClip(m_RecordingClip, recordedSamples);
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
}
