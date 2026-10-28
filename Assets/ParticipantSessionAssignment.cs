using System;
using System.Text.RegularExpressions;

/// <summary>Minimal desktop-to-headset contract. Contains no interview or identifying name.</summary>
[Serializable]
public sealed class ParticipantSessionAssignment
{
    public int schema = 1, neutralOption;
    public string id, participantCode, configuration, neutralGender, cloneVoiceId;
    public ParticipantFitMeasurements fit = new ParticipantFitMeasurements();
    public bool NeutralFirst => configuration == "B";
    public string Order => NeutralFirst ? "neutral_then_selfsimilar" : "selfsimilar_then_neutral";
    public void Validate()
    {
        fit?.Validate(); // Legacy assignments have no fit data.
        if (schema != 1 || !Guid.TryParseExact(id, "N", out _) ||
            !Regex.IsMatch(participantCode ?? "", @"^P[0-9]{3,12}$") ||
            (configuration != "A" && configuration != "B") ||
            (neutralGender != "Male" && neutralGender != "Female") || neutralOption < 0 || neutralOption >= 6 ||
            !Regex.IsMatch(cloneVoiceId ?? "", @"^[A-Za-z0-9_-]{8,128}$"))
            throw new ArgumentException("Invalid participant session assignment.");
    }
}

/// <summary>Manually entered binocular distance IPD; never inferred from tracked eye origins.</summary>
[Serializable]
public sealed class ParticipantFitMeasurements
{
    public bool ipdRecorded, headsetIpdRecorded;
    public float participantIpdMm, headsetIpdMm;
    public string ipdSource = "not_recorded";
    public void Validate()
    {
        bool sourceKnown = ipdSource == "pupillometer" || ipdSource == "ruler" || ipdSource == "participant_report";
        if (ipdRecorded ? !sourceKnown || !ValidMm(participantIpdMm) : participantIpdMm != 0 || ipdSource != "not_recorded")
            throw new ArgumentException("Enter binocular distance IPD in millimetres and its source, or mark it not recorded.");
        if (headsetIpdRecorded ? !ValidMm(headsetIpdMm) : headsetIpdMm != 0)
            throw new ArgumentException("Enter the headset lens spacing in millimetres, or leave it blank.");
    }
    // Broad input/unit sanity bound, not a physiological inclusion criterion.
    static bool ValidMm(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0 && value <= 100;
    public ParticipantFitMeasurements Copy() => (ParticipantFitMeasurements)MemberwiseClone();
}
