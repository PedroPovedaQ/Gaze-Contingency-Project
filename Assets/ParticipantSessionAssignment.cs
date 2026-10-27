using System;
using System.Text.RegularExpressions;

/// <summary>Minimal desktop-to-headset contract. Contains no interview or identifying name.</summary>
[Serializable]
public sealed class ParticipantSessionAssignment
{
    public int schema = 1, neutralOption;
    public string id, participantCode, configuration, neutralGender, cloneVoiceId;
    public bool NeutralFirst => configuration == "B";
    public string Order => NeutralFirst ? "neutral_then_selfsimilar" : "selfsimilar_then_neutral";
    public void Validate()
    {
        if (schema != 1 || !Guid.TryParseExact(id, "N", out _) ||
            !Regex.IsMatch(participantCode ?? "", @"^P[0-9]{3,12}$") ||
            (configuration != "A" && configuration != "B") ||
            (neutralGender != "Male" && neutralGender != "Female") || neutralOption < 0 || neutralOption >= 6 ||
            !Regex.IsMatch(cloneVoiceId ?? "", @"^[A-Za-z0-9_-]{8,128}$"))
            throw new ArgumentException("Invalid participant session assignment.");
    }
}
