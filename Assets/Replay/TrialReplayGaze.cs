using UnityEngine;

/// <summary>Capture the selected and pre-alignment rays in the same fixed recording frame.</summary>
public static class TrialReplayGaze
{
    public const string Version = "recorded_used_and_unaligned_ray_v1";

    public static void Capture(TrialReplayRecord record, GazeObservation gaze, bool usable,
        Vector3 recordingOrigin, Quaternion recordingRotation)
    {
        record.gazeRayVersion = Version;
        record.gazeAvailable = gaze != null && gaze.deviceAvailable;
        record.gazeTracked = usable ? 1 : 0;
        if (gaze == null) return;
        Quaternion inverse = Quaternion.Inverse(recordingRotation);
        if (usable)
        {
            record.gazeOrigin = inverse * (gaze.worldOrigin - recordingOrigin);
            record.gazeDirection = inverse * gaze.worldDirection;
            record.gazeUnalignedAvailable = gaze.unalignedWorldDirection.sqrMagnitude > .5f;
            if (record.gazeUnalignedAvailable)
            {
                record.gazeUnalignedOrigin = inverse * (gaze.unalignedWorldOrigin - recordingOrigin);
                record.gazeUnalignedDirection = inverse * gaze.unalignedWorldDirection;
            }
        }
        record.gazeObservationSequence = gaze.sequence; record.gazeReceiptTime = gaze.receiptTime;
        record.gazeRecordingId = gaze.recordingId;
        record.gazeInputEventTime = gaze.inputEventTime; record.gazeDeliveryAge = gaze.ageSeconds;
        record.gazeSource = gaze.source; record.gazeRaySource = gaze.raySource; record.gazeInvalidReason = gaze.invalidReason;
        record.gazeAlignmentId = gaze.alignmentId; record.gazeAlignmentStatus = gaze.alignmentStatus;
        record.gazeAlignmentApplied = usable && gaze.alignmentApplied;
        record.gazeAlignmentYawDegrees = gaze.alignmentYawDegrees;
        record.gazeAlignmentPitchDegrees = gaze.alignmentPitchDegrees;
    }
}
