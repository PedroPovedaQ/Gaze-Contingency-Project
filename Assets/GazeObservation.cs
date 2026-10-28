using System;
using UnityEngine;

/// <summary>Immutable-by-convention acquisition snapshot; unknown sensor timing stays explicit.</summary>
[Serializable]
public sealed class GazeObservation
{
    public string validationProtocol, validationStage, validationCondition;
    public int validationStep = -1;
    public HtcQueryTiming htcTiming;
    public bool htcHeadPoseValid;
    public Quaternion htcHeadWorldRotation=Quaternion.identity;
    public double htcQueryEndReceiptTime, headPoseReceiptTime;
    public string headTimeSemantics = "Unity_camera_pose_at_receipt_not_sensor_synchronized";
    public bool taskAoiAvailable;
    public string taskTargetId, leftHitObjectId, rightHitObjectId;
    public string taskAoiDefinition="closest_layer8_interaction_collider_including_gaze_hitbox_expansion";
    public Vector3 taskTargetPosition, taskTargetBoundsCenter, taskTargetBoundsExtents;
    public int schema = 1, frame, deviceId, round = -1, block = -1, run;
    public long sequence;
    public string kind = "gaze_observation", source = "OpenXR_EXT_InputSystem_pose",
        timeSemantics = "input_state_delivery_not_sensor_capture", coordinateFrame = "Unity_world_metres",
        participant, sourceTrialId, phase, voice, invalidReason, validationId, validationSet, targetId,
        correction = "none", recordingId;
    // source/raw*/tracked describe EXT acquisition; raySource identifies worldOrigin/worldDirection.
    // HTC fallback is a derived binocular mean, never a claimed native combined-gaze sample.
    public string raySource = "OpenXR_EXT";
    // Preserve the selected EXT/native ray before any participant alignment, independently of raw EXT.
    public Vector3 unalignedWorldOrigin, unalignedWorldDirection;
    public string alignmentId, alignmentStatus = "none";
    public bool alignmentApplied;
    public bool eyeDotEnabled;
    public int activationStep = -1;
    public float alignmentYawDegrees, alignmentPitchDegrees;
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
    public float usedTargetErrorDegrees, targetErrorDegrees, mirroredOriginErrorDegrees, maxDeliveryAgeSeconds;
}
