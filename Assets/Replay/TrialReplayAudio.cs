using System;
using System.IO;
using UnityEngine;

public static class TrialReplayAudio
{
    public static byte[] Encode(float[] samples, int channels, int rate)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)channels); writer.Write(rate); writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2)); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
            foreach (float s in samples) writer.Write((short)(Math.Max(-1, Math.Min(1, s)) * 32767));
            return stream.ToArray();
        }
    }

    public static float[] Decode(string path, out int channels, out int rate)
    {
        using (var reader = new BinaryReader(File.OpenRead(path)))
        {
            string riff = new string(reader.ReadChars(4)); reader.ReadInt32();
            if (riff != "RIFF" || new string(reader.ReadChars(8)) != "WAVEfmt " || reader.ReadInt32() != 16 || reader.ReadInt16() != 1)
                throw new InvalidDataException("Expected replay PCM16 WAV.");
            channels = reader.ReadInt16(); rate = reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt16();
            if (reader.ReadInt16() != 16 || new string(reader.ReadChars(4)) != "data" || channels < 1 || channels > 2 || rate < 8000 || rate > 192000)
                throw new InvalidDataException("Invalid replay WAV format.");
            int bytes = reader.ReadInt32();
            if (bytes < 0 || bytes > 128 * 1024 * 1024 || bytes % (2 * channels) != 0 || bytes != reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid replay WAV length.");
            var samples = new float[bytes / 2];
            for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
            return samples;
        }
    }
}
