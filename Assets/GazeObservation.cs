using System;
using UnityEngine;

/// <summary>Immutable-by-convention acquisition snapshot; unknown sensor timing stays explicit.</summary>
[Serializable]
public sealed class GazeObservation
{
    public int schema = 1, frame, deviceId, round = -1, block = -1, run;
    public long sequence;
    public string kind = "gaze_observation", source = "OpenXR_EXT_InputSystem_pose",
        timeSemantics = "input_state_delivery_not_sensor_capture", coordinateFrame = "Unity_world_metres",
        participant, sourceTrialId, phase, voice, invalidReason, validationId, validationSet, targetId,
        correction = "none", recordingId;
    public double receiptTime, inputEventTime, ageSeconds, htcQueryReceiptTime;
    public bool sourceCaptureTimeKnown, sourceSequenceKnown, newInputUpdate, deviceAvailable,
        tracked, poseValid, valid, focused, headValid, validationTargetPresent, practice;
    public Vector3 rawLocalOrigin, rawWorldOrigin, rawWorldDirection, worldOrigin, worldDirection,
        headPosition, trackingOriginPosition, validationTarget;
    public Quaternion rawLocalRotation = Quaternion.identity, headRotation = Quaternion.identity,
        trackingOriginRotation = Quaternion.identity;
    public bool htcGazeAvailable, leftValid, rightValid, pupilAvailable, leftPupilValid, rightPupilValid,
        opennessAvailable, leftOpennessValid, rightOpennessValid;
    // Unavailable values have false validity flags; CSV export writes empty cells, never measured zero.
    public Vector3 leftNativeOrigin, rightNativeOrigin, leftWorldOrigin, rightWorldOrigin, leftWorldDirection, rightWorldDirection;
    public Quaternion leftNativeRotation = Quaternion.identity, rightNativeRotation = Quaternion.identity;
    public float leftPupilMm, rightPupilMm, leftOpenness, rightOpenness;
    public float targetErrorDegrees, mirroredOriginErrorDegrees, maxDeliveryAgeSeconds;
}
