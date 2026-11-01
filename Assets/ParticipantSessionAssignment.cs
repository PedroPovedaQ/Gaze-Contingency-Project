using System;
using System.Text.RegularExpressions;

/// <summary>Minimal desktop-to-headset contract. Contains no interview or identifying name.</summary>
[Serializable]
public sealed class ParticipantSessionAssignment
{
    public int schema = 1, neutralOption;
    public string id, participantCode, configuration, neutralGender, cloneVoiceId;
    public string consentId, intakeProtocol, cacheManifestSha256;
    public bool eligibilityReviewed, development;
    public ParticipantFitMeasurements fit = new ParticipantFitMeasurements();
    public bool NeutralFirst => configuration == "B";
    public string Order => NeutralFirst ? "neutral_then_selfsimilar" : "selfsimilar_then_neutral";
    public void Validate()
    {
        fit?.Validate(); // Legacy assignments have no fit data.
        if ((schema != 1 && schema != 2) || !Guid.TryParseExact(id, "N", out _) ||
            !Regex.IsMatch(participantCode ?? "", @"^P[0-9]{3,12}$") ||
            (configuration != "A" && configuration != "B") ||
            (neutralGender != "Male" && neutralGender != "Female") || neutralOption < 0 || neutralOption >= 6 ||
            !Regex.IsMatch(cloneVoiceId ?? "", @"^[A-Za-z0-9_-]{8,128}$"))
            throw new ArgumentException("Invalid participant session assignment.");
        if (schema == 2 && (!Guid.TryParseExact(consentId, "N", out _) || !eligibilityReviewed ||
            (intakeProtocol != "audio-intake-v2-20261028" && intakeProtocol != "audio-intake-v3-20261101" && intakeProtocol != "audio-intake-v4-20261101") || !Regex.IsMatch(cacheManifestSha256 ?? "", @"^[a-f0-9]{64}$")))
            throw new ArgumentException("Consent, eligibility review and prepared audio receipt are required.");
    }
}

/// <summary>Legacy manual measurements and separately labelled, unvalidated automatic estimates.</summary>
[Serializable]
public sealed class ParticipantFitMeasurements
{
    public bool ipdRecorded, headsetIpdRecorded;
    public float participantIpdMm, headsetIpdMm;
    public string ipdSource = "not_recorded";
    public FitSeparationMeasurement viewSeparation, eyeSeparation;
    public string fitCaptureId, fitStartedUtc, fitEndedUtc, fitWarning;
    public string autoIpdStatus = "unavailable", separationChange = "insufficient_observation";
    public string separationChangeUtc, separationChangeSource, manualRecordedUtc;
    public float separationChangeMm;
    public int setupSampleCount;
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
    public ParticipantFitMeasurements Copy()
    {
        var copy = (ParticipantFitMeasurements)MemberwiseClone();
        copy.viewSeparation = viewSeparation?.Copy(); copy.eyeSeparation = eyeSeparation?.Copy();
        return copy;
    }
}

[Serializable]
public sealed class FitSeparationMeasurement
{
    public bool recorded;
    public float millimetres, rangeMm, windowSeconds;
    public int samples;
    public string source, method = "median_stable_window_v1", recordedUtc;
    public FitSeparationMeasurement Copy() => (FitSeparationMeasurement)MemberwiseClone();
}
