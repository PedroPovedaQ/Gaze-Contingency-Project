using System;

/// <summary>
/// A subtle pilot treatment for cloned speech: softened highs and two quiet,
/// short reflections. Process PCM once before the existing RMS/headroom matching.
/// No pitch shift, onset delay, extended tail, or effect state between phrases.
/// </summary>
public static class InnerThoughtVoice
{
    public const string Version = "inner-thought-v1";
    public static string Profile(bool selfSimilar) => selfSimilar ? Version : "dry";

    public static void Process(float[] samples, int channels, int sampleRate, bool selfSimilar)
    {
        // Neutral speech must remain bit-for-bit unchanged by this treatment.
        if (!selfSimilar) return;
        if (samples == null || channels < 1 || sampleRate < 1 || samples.Length % channels != 0)
            throw new ArgumentException("Invalid PCM format for inner-thought voice.");
        int frames = samples.Length / channels;
        int near = Math.Max(1, (int)Math.Round(sampleRate * 0.024));
        int far = Math.Max(1, (int)Math.Round(sampleRate * 0.052));
        float alpha = (float)(1 - Math.Exp(-2 * Math.PI * 2400 / sampleRate));
        var softened = new float[frames];
        for (int channel = 0; channel < channels; channel++)
        {
            float low = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                int index = frame * channels + channel;
                float dry = samples[index];
                low += alpha * (dry - low);
                softened[frame] = low;
                // Dry voice remains dominant; taps have no feedback/repeating echo.
                float wet = (frame >= near ? softened[frame - near] * 0.14f : 0)
                    + (frame >= far ? softened[frame - far] * 0.07f : 0);
                samples[index] = dry * 0.6f + low * 0.4f + wet;
            }
        }
    }
}
