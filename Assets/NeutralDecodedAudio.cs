using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>Disposable decode cache, never a replacement for the provider MP3 or final level matching.</summary>
public static class NeutralDecodedAudio
{
    public const string Suffix = ".decoded-v1";
    const int Magic = 0x3143504e; // NPC1, little endian; shared with neutral_audio.py.
    public static void Save(string source, float[] samples, int channels, int frequency)
    {
        string pending = source + Suffix + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] pcm = new byte[samples.Length * 4]; Buffer.BlockCopy(samples, 0, pcm, 0, pcm.Length);
            using (var sha = SHA256.Create())
            using (var file = File.Create(pending))
            using (var writer = new BinaryWriter(file))
            {
                writer.Write(Magic); writer.Write(channels); writer.Write(frequency); writer.Write(samples.Length);
                writer.Write(sha.ComputeHash(File.ReadAllBytes(source))); writer.Write(sha.ComputeHash(pcm)); writer.Write(pcm);
            }
            if (File.Exists(source + Suffix)) File.Delete(source + Suffix);
            File.Move(pending, source + Suffix);
        }
        finally { if (File.Exists(pending)) File.Delete(pending); }
    }

    public static bool TryLoad(string source, out float[] samples, out int channels, out int frequency)
    {
        samples = null; channels = frequency = 0;
        try
        {
            using (var file = File.OpenRead(source + Suffix))
            using (var reader = new BinaryReader(file))
            using (var sha = SHA256.Create())
            {
                if (reader.ReadInt32() != Magic) return false;
                int ch = reader.ReadInt32(), hz = reader.ReadInt32(), count = reader.ReadInt32();
                if (ch < 1 || ch > 2 || hz < 8000 || hz > 96000 || count < hz * ch / 10 ||
                    count > hz * ch * 120 || count % ch != 0 || file.Length != 80L + count * 4L) return false;
                if (!Equal(reader.ReadBytes(32), sha.ComputeHash(File.ReadAllBytes(source)))) return false;
                byte[] expected = reader.ReadBytes(32), pcm = reader.ReadBytes(count * 4);
                if (!Equal(expected, sha.ComputeHash(pcm))) return false;
                var decoded = new float[count]; Buffer.BlockCopy(pcm, 0, decoded, 0, pcm.Length);
                foreach (float value in decoded) if (float.IsNaN(value) || float.IsInfinity(value)) return false;
                samples = decoded; channels = ch; frequency = hz; return true;
            }
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
