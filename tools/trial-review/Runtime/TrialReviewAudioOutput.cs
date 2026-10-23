using System;
using System.Collections.Generic;
using UnityEngine;

// Local saved WAV playback only. Never call the study's voice providers.
public sealed class TrialReviewAudioOutput : TrialReplayAudioPreview.IOutput, IDisposable
{
    readonly HashSet<AudioSource> m_Sources = new HashSet<AudioSource>();
    public object Play(float[] samples, int channels, int rate, int offsetFrames, string name)
    {
        var clip = AudioClip.Create(name, samples.Length / channels, channels, rate, false);
        var go = new GameObject(name);
        try
        {
            if (!clip.SetData(samples, 0)) throw new InvalidOperationException("Cannot load recorded audio");
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.clip = clip;
            source.timeSamples = offsetFrames; source.Play();
            m_Sources.Add(source);
            Debug.Log($"[TrialReviewAudio] start {name}; offset_frames={offsetFrames}; frames={samples.Length/channels}; rate={rate}");
            return source;
        }
        catch { UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(clip); throw; }
    }
    public void Stop(object handle)
    {
        var source = (AudioSource)handle;
        if (!m_Sources.Remove(source)) return;
        source.Stop(); UnityEngine.Object.Destroy(source.clip); UnityEngine.Object.Destroy(source.gameObject);
    }
    public void Dispose() { foreach (var source in new List<AudioSource>(m_Sources)) Stop(source); }
}
