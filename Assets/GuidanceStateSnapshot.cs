using System;

/// <summary>Recorded local guidance state. Times use Unity realtime, not replay-relative time.
/// Missing snapshot means not recorded; empty hit with valid evidence means no object.</summary>
[Serializable]
public sealed class GuidanceStateSnapshot
{
    public string version = "guidance-state-v2";
    public string guidancePolicy; // Null in older recordings means unavailable, not baseline.
    public string phase, previousPhase, transitionReason, allowedAction;
    public long revision, observationSequence = -1;
    public double enteredAtSeconds, capturedAtSeconds;
    public bool gazeValid, controllerValid;
    // New automatic guidance only; does not mute setup/success or cut active speech.
    public bool newGuidanceAllowed;
    public string gazeHitObjectId, vicinityWinnerObjectId, controllerHitObjectId, targetObjectId;
}
