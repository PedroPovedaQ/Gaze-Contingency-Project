using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Minimal encoder: Unity float samples -> 16-bit PCM WAV byte buffer.
/// Used to package a microphone recording for the Voxtral clone endpoint.
/// </summary>
public static class WavUtility
{
    /// <summary>Encode raw mono/interleaved float samples as a 16-bit PCM WAV.</summary>
    public static byte[] EncodePcm16(float[] samples, int sampleRate, int channels)
    {
        if (samples == null) samples = Array.Empty<float>();
        int byteRate = sampleRate * channels * 2;
        int dataSize = samples.Length * 2;

        using (var mem = new MemoryStream(44 + dataSize))
        using (var w = new BinaryWriter(mem))
        {
            // RIFF header
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            // fmt chunk
            w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16);                 // PCM chunk size
            w.Write((short)1);           // audio format = PCM
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)(channels * 2)); // block align
            w.Write((short)16);          // bits per sample
            // data chunk
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(dataSize);
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), -32768, 32767);
                w.Write(s);
            }
            w.Flush();
            return mem.ToArray();
        }
    }

    /// <summary>
    /// Extract the recorded portion of a Microphone clip (up to <paramref name="sampleCount"/>
    /// frames) and encode it as WAV. Pass Microphone.GetPosition() as sampleCount.
    /// </summary>
    public static byte[] EncodeFromClip(AudioClip clip, int sampleCount)
    {
        if (clip == null) return Array.Empty<byte>();
        int frames = Mathf.Clamp(sampleCount, 0, clip.samples);
        if (frames <= 0) frames = clip.samples;
        var data = new float[frames * clip.channels];
        clip.GetData(data, 0);
        return EncodePcm16(data, clip.frequency, clip.channels);
    }
}
