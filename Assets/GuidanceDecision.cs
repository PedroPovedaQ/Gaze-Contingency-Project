using System;

/// <summary>One immutable-by-convention guidance audit snapshot. References raw eye
/// observations rather than claiming that a nearby replay frame caused the cue.</summary>
[Serializable]
public sealed class GuidanceDecision
{
    public int schema = 1;
    public GuidanceStateSnapshot guidanceState;
    // Optional target-bearing evidence for current and future audits. Empty on historical records.
    public bool targetGeometryAvailable;
    public float targetYawError, targetPitchError;
    public float turnRemainingDegrees;
    public double recentTargetVisitReceiptTime = -1;
    public long recentTargetVisitObservationSequence = -1;
    public bool verticalGuidanceMeasured;
    public float verticalErrorMetres, guidancePlaneHeightMetres, verticalErrorPlaneFraction;
    public bool gazeOnTarget, targetVicinityMeasured, controllerEvidenceValid, controllerOnTarget, devVicinityRingEnabled;
    public float targetVicinityErrorDegrees, targetVicinityRadiusDegrees, targetVicinityPaddingDegrees;
    public string gazeVicinityWinnerObjectId;
    public int gazeVicinityCandidateCount;
    public bool gazeVicinityAmbiguous;
    public float gazeVicinityClosestErrorDegrees;
    public string targetObjectId, controllerTargetObjectId;
    public int controllerTelemetryFrame = -1;
    public double controllerTelemetryTime;
    public string targetHistoryPolicy = "target-bearing-crossing-v1";
    public GazeTargetHistory.Sample[] targetHistory;
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
