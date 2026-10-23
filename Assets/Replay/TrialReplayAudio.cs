using System;
using System.IO;
using UnityEngine;

public static class TrialReplayAudio
{
    public struct Info { public int Channels, Rate, Frames; }
    public static Info Inspect(string path)
    {
        using (var reader = new BinaryReader(File.OpenRead(path))) return ReadHeader(reader);
    }
    static Info ReadHeader(BinaryReader reader)
    {
        long size = reader.BaseStream.Length;
        if (size < 44 || size > 128L * 1024 * 1024 + 44)
            throw new InvalidDataException("Invalid replay WAV file size.");
        string riff = new string(reader.ReadChars(4)); int riffBytes = reader.ReadInt32();
        if (riff != "RIFF" || riffBytes != size - 8 || new string(reader.ReadChars(8)) != "WAVEfmt " ||
            reader.ReadInt32() != 16 || reader.ReadInt16() != 1)
            throw new InvalidDataException("Expected canonical replay PCM16 WAV.");
        int channels = reader.ReadInt16(), rate = reader.ReadInt32(), byteRate = reader.ReadInt32(), alignment = reader.ReadInt16();
        if (reader.ReadInt16() != 16 || new string(reader.ReadChars(4)) != "data" || channels < 1 || channels > 2 || rate < 8000 || rate > 192000 ||
            alignment != channels * 2 || byteRate != rate * alignment)
            throw new InvalidDataException("Invalid replay WAV format/rates.");
        int bytes = reader.ReadInt32();
        if (bytes <= 0 || bytes % alignment != 0 || bytes != size - 44)
            throw new InvalidDataException("Invalid replay WAV length.");
        return new Info { Channels=channels, Rate=rate, Frames=bytes/alignment };
    }
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
            var info = ReadHeader(reader); channels = info.Channels; rate = info.Rate;
            var samples = new float[info.Frames * channels];
            for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
            return samples;
        }
    }
}
