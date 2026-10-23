using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

/// <summary>Script checks and exact quarantines, not an automatic speech-recognition guarantee.</summary>
public static class VoiceAudioIntegrity
{
    static readonly Regex Fillers = new Regex(@"\b(?:u+m+|u+h+|ugh+|h+m+)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static bool HasForbiddenFiller(string text) => text != null && Fillers.IsMatch(text);

    // Neutral Eric clips from the 2026-10-11 DEV_NEUTRAL run. Two local ASR models
    // independently found extra words matching the user's reported gibberish.
    public static bool IsRejectedHash(string hash) =>
        string.Equals(hash, "fc0e8723298e34f4e275d200db18ac36276f763a62bf1d55fbc8fc4980e6f450", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(hash, "eef46d428fdadf810af0006cafcd8661949b0fa74126e8d82ab04876fb9d0bc2", StringComparison.OrdinalIgnoreCase);

    public static bool IsRejectedAudio(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return IsRejectedHash(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""));
    }

    public static bool UsableCache(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            if (!IsRejectedAudio(File.ReadAllBytes(path))) return true;
            File.Move(path, path + ".rejected-" + Guid.NewGuid().ToString("N"));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        return false;
    }
}
