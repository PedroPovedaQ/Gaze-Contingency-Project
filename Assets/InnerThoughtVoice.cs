using System;

/// <summary>
/// "Soft-clone close" inner-thought treatment for self-similar speech. The inner
/// quality comes mainly from the performance: the clone is enrolled from a soft,
/// quiet reading (see VoiceModeSelector). This pass only adds close-mic polish:
///   - 80 Hz rumble cut, +3 dB proximity warmth at 200 Hz, -1.5 dB at 3 kHz;
///   - gentle 2.5:1 levelling above 1.4x the phrase's own RMS (5 ms / 150 ms);
///   - a barely-there dark stereo room (0.35 s RT60, 3.5 kHz, -24 dB) with a
///     0.4 s tail appended so it can decay.
/// Speech starts at sample 0 (no onset delay); output is stereo. Seeds are fixed,
/// so every phrase renders identically. Neutral speech is returned unchanged.
/// </summary>
public static class InnerThoughtVoice
{
    public const string Version = "inner-thought-v2-soft-close";
    const float k_TailSeconds = 0.4f, k_RoomRt60 = 0.35f, k_RoomDb = -24f;

    public static string Profile(bool selfSimilar) => selfSimilar ? Version : "dry";

    public struct Result
    {
        public float[] Samples;
        public int Channels;
        /// <summary>Interleaved samples covering the words; level matching measures only these.</summary>
        public int SpeechSamples;
    }

    public static Result Process(float[] samples, int channels, int sampleRate, bool selfSimilar)
    {
        // Neutral speech must remain bit-for-bit unchanged by this treatment.
        if (!selfSimilar)
            return new Result { Samples = samples, Channels = channels, SpeechSamples = samples?.Length ?? 0 };
        if (samples == null || channels < 1 || sampleRate < 1 || samples.Length % channels != 0)
            throw new ArgumentException("Invalid PCM format for inner-thought voice.");

        int frames = samples.Length / channels;
        int total = frames + (int)Math.Round(sampleRate * k_TailSeconds);
        var voice = new float[total];
        for (int f = 0; f < frames; f++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += samples[f * channels + c];
            voice[f] = sum / channels;
        }

        float sr = sampleRate;
        foreach (var filter in new[] { Biquad.HighPass(sr, 80f), Biquad.LowShelf(sr, 200f, 3f), Biquad.Peak(sr, 3000f, 1f, -1.5f) })
            filter.Run(voice);
        Level(voice, frames, sr);

        var left = Convolve(voice, RoomImpulse(sr, 0x2545F491u));
        var right = Convolve(voice, RoomImpulse(sr, 0x5BD1E995u));
        float wet = (float)Math.Pow(10, k_RoomDb / 20.0);
        var output = new float[total * 2];
        for (int i = 0; i < total; i++)
        {
            output[i * 2] = voice[i] + left[i] * wet;
            output[i * 2 + 1] = voice[i] + right[i] * wet;
        }
        return new Result { Samples = output, Channels = 2, SpeechSamples = frames * 2 };
    }

    // Gentle 2.5:1 levelling relative to the phrase's own RMS, so soft reads stay even.
    static void Level(float[] x, int frames, float sr)
    {
        double sum = 0;
        for (int i = 0; i < frames; i++) sum += x[i] * x[i];
        float threshold = 1.4f * (float)Math.Sqrt(sum / Math.Max(1, frames));
        if (threshold <= 0) return;
        float attack = (float)Math.Exp(-1.0 / (0.005 * sr)), release = (float)Math.Exp(-1.0 / (0.150 * sr));
        float env = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float v = Math.Abs(x[i]);
            env = v > env ? attack * env + (1 - attack) * v : release * env + (1 - release) * v;
            if (env > threshold) x[i] *= (float)Math.Pow(env / threshold, 1.0 / 2.5 - 1.0);
        }
    }

    // Decaying low-passed noise with an 8 ms gap: a small, dark, decorrelated room per ear.
    static float[] RoomImpulse(float sr, uint seed)
    {
        var ir = new float[(int)(k_RoomRt60 * sr)];
        uint state = seed;
        for (int i = 0; i < ir.Length; i++)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            ir[i] = state / (float)uint.MaxValue * 2f - 1f;
        }
        Biquad.LowPass(sr, 3500f).Run(ir);
        int gap = (int)(0.008f * sr);
        double energy = 0;
        for (int i = 0; i < ir.Length; i++)
        {
            ir[i] = i < gap ? 0 : ir[i] * (float)Math.Exp(-6.9 * i / (k_RoomRt60 * sr));
            energy += ir[i] * ir[i];
        }
        float norm = energy > 0 ? (float)(1 / Math.Sqrt(energy)) : 0;
        for (int i = 0; i < ir.Length; i++) ir[i] *= norm;
        return ir;
    }

    // FFT convolution truncated to the input length.
    static float[] Convolve(float[] x, float[] h)
    {
        int n = 1;
        while (n < x.Length + h.Length) n <<= 1;
        var xr = new double[n]; var xi = new double[n];
        var hr = new double[n]; var hi = new double[n];
        for (int i = 0; i < x.Length; i++) xr[i] = x[i];
        for (int i = 0; i < h.Length; i++) hr[i] = h[i];
        Fft(xr, xi, false); Fft(hr, hi, false);
        for (int i = 0; i < n; i++)
        {
            double re = xr[i] * hr[i] - xi[i] * hi[i];
            xi[i] = xr[i] * hi[i] + xi[i] * hr[i];
            xr[i] = re;
        }
        Fft(xr, xi, true);
        var y = new float[x.Length];
        for (int i = 0; i < y.Length; i++) y[i] = (float)(xr[i] / n);
        return y;
    }

    // In-place iterative radix-2 FFT; inverse is unscaled.
    static void Fft(double[] re, double[] im, bool inverse)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }
        for (int size = 2; size <= n; size <<= 1)
        {
            double angle = (inverse ? 2 : -2) * Math.PI / size;
            double wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int start = 0; start < n; start += size)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < size / 2; k++)
                {
                    int a = start + k, b = a + size / 2;
                    double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                    re[b] = re[a] - tr; im[b] = im[a] - ti;
                    re[a] += tr; im[a] += ti;
                    double next = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = next;
                }
            }
        }
    }

    // RBJ audio-EQ-cookbook biquad, transposed direct form II.
    sealed class Biquad
    {
        readonly float b0, b1, b2, a1, a2;

        Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            this.b0 = (float)(b0 / a0); this.b1 = (float)(b1 / a0); this.b2 = (float)(b2 / a0);
            this.a1 = (float)(a1 / a0); this.a2 = (float)(a2 / a0);
        }

        public void Run(float[] x)
        {
            float z1 = 0, z2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float input = x[i];
                float output = b0 * input + z1;
                z1 = b1 * input - a1 * output + z2;
                z2 = b2 * input - a2 * output;
                x[i] = output;
            }
        }

        static void Prepare(float sr, float hz, float q, out double cos, out double alpha)
        {
            double w = 2 * Math.PI * Math.Min(hz, sr * 0.49f) / sr;
            cos = Math.Cos(w);
            alpha = Math.Sin(w) / (2 * q);
        }

        public static Biquad LowPass(float sr, float hz)
        {
            Prepare(sr, hz, 0.707f, out var c, out var a);
            return new Biquad((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public static Biquad HighPass(float sr, float hz)
        {
            Prepare(sr, hz, 0.707f, out var c, out var a);
            return new Biquad((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a);
        }

        public static Biquad Peak(float sr, float hz, float q, float db)
        {
            Prepare(sr, hz, q, out var c, out var a);
            double g = Math.Pow(10, db / 40);
            return new Biquad(1 + a * g, -2 * c, 1 - a * g, 1 + a / g, -2 * c, 1 - a / g);
        }

        public static Biquad LowShelf(float sr, float hz, float db)
        {
            Prepare(sr, hz, 0.707f, out var c, out var alpha);
            double g = Math.Pow(10, db / 40), s = 2 * Math.Sqrt(g) * alpha;
            return new Biquad(g * ((g + 1) - (g - 1) * c + s), 2 * g * ((g - 1) - (g + 1) * c), g * ((g + 1) - (g - 1) * c - s),
                (g + 1) + (g - 1) * c + s, -2 * ((g - 1) + (g + 1) * c), (g + 1) + (g - 1) * c - s);
        }
    }
}
