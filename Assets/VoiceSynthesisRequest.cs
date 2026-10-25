using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

/// <summary>Bounded repair settings shared by neutral and cloned voices. The voice
/// and model never change; the verifier still checks against the original script.</summary>
public static class VoiceSynthesisRequest
{
    public static string Json(string text, string model, int attempt = 0)
    {
        // Say the decimal separator explicitly; otherwise 47.5 can be rendered as
        // two separate integers. Keep the original script as the verification target.
        string spokenText = ExpandDecimals(text);
        if (attempt > 0) spokenText = ExpandIntegers(spokenText);
        // Eric can fuse the /s/ in "look slightly" into "looks slightly" even at
        // maximum stability. A final-attempt pause separates the words; the same
        // verbatim ASR gate still verifies against the untouched original script.
        if (attempt >= 2)
            spokenText = Regex.Replace(spokenText ?? "", @"\b(look)\s+(slightly)\b", "$1, $2", RegexOptions.IgnoreCase);
        var body = new JObject { ["text"] = spokenText, ["model_id"] = model };
        if (attempt > 0)
        {
            body["language_code"] = "en";
            body["voice_settings"] = new JObject {
                ["stability"] = attempt == 1 ? .85f : 1f,
                ["similarity_boost"] = .75f, ["style"] = 0f,
                ["use_speaker_boost"] = attempt == 1, ["speed"] = 1f };
        }
        return body.ToString(Newtonsoft.Json.Formatting.None);
    }

    // Expand only whole integers. Do not turn 1.5 into "one.five" or alter IDs.
    public static string ExpandIntegers(string text) => Regex.Replace(text ?? "",
        @"(?<![\p{L}\p{N}.])\d{1,3}(?![\p{L}\p{N}.])", m => NumberWords(int.Parse(m.Value)));

    public static string ExpandDecimals(string text) => Regex.Replace(text ?? "",
        @"(?<![\p{L}\p{N}.])(?<whole>\d{1,3})\.(?<fraction>\d+)(?![\p{L}\p{N}]|\.\d)", m =>
        {
            string words = NumberWords(int.Parse(m.Groups["whole"].Value)) + " point";
            foreach (char digit in m.Groups["fraction"].Value) words += " " + NumberWords(digit - '0');
            return words;
        });

    public static string NumberWords(int value)
    {
        if (value < 0 || value > 999) throw new ArgumentOutOfRangeException(nameof(value));
        string[] small = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
            "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
        string[] tens = { "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };
        if (value < 20) return small[value];
        if (value < 100) return tens[value / 10] + (value % 10 == 0 ? "" : " " + small[value % 10]);
        return small[value / 100] + " hundred" + (value % 100 == 0 ? "" : " " + NumberWords(value % 100));
    }
}
