using System;

/// <summary>One immutable-by-convention guidance audit snapshot. References raw eye
/// observations rather than claiming that a nearby replay frame caused the cue.</summary>
[Serializable]
public sealed class GuidanceDecision
{
    public int schema = 1;
    public string decisionId, stage, reason, category, phrase, policyVersion,
        uncertaintyStatus = "not_estimated_pilot_thresholds",
        eyeRecordingId, participant, sourceTrialId, source, phase;
    public long observationSequence = -1;
    public double decisionTime, gazeReceiptTime, gazeInputEventTime, gazeAgeAtDecision;
    public bool observationAvailable, gazeDeviceAvailable, gazeDeliveryTimeKnown,
        gazeUsable, insideTargetWall, farTurn, candidateStable;
    public float maxDeliveryAgeSeconds;
    public int run, round = -1, block = -1;
    public string invalidReason;

    public static GuidanceDecision Capture(string id, string stage, string reason,
        string category, string phrase, GazeObservation sample, double now,
        bool usable, bool inside, bool far, bool stable)
    {
        var result = new GuidanceDecision {
            decisionId = id, stage = stage, reason = reason, category = category,
            phrase = phrase, policyVersion = DirectionalHintPolicy.Version,
            decisionTime = now, observationAvailable = sample != null, gazeUsable = usable,
            insideTargetWall = inside, farTurn = far, candidateStable = stable
        };
        if (sample == null) { result.invalidReason = "no_gaze_observation"; return result; }
        result.eyeRecordingId = sample.recordingId;
        result.observationSequence = sample.sequence;
        result.gazeReceiptTime = sample.receiptTime;
        result.gazeInputEventTime = sample.inputEventTime;
        result.gazeDeviceAvailable = sample.deviceAvailable;
        result.gazeDeliveryTimeKnown = sample.deviceAvailable && sample.inputEventTime > 0;
        result.maxDeliveryAgeSeconds = sample.maxDeliveryAgeSeconds;
        result.gazeAgeAtDecision = result.gazeDeliveryTimeKnown ? Math.Max(0, now - sample.inputEventTime) : 0;
        result.participant = sample.participant; result.run = sample.run;
        result.round = sample.round; result.block = sample.block;
        result.sourceTrialId = sample.sourceTrialId; result.phase = sample.phase;
        result.source = sample.source;
        result.invalidReason = usable ? "" : string.IsNullOrEmpty(sample.invalidReason)
            ? "unusable_at_guidance_evaluation" : sample.invalidReason;
        return result;
    }
}
