using System;

/// <summary>Recorded local guidance state. Times use Unity realtime, not replay-relative time.
/// Missing snapshot means not recorded; empty hit with valid evidence means no object.</summary>
[Serializable]
public sealed class GuidanceStateSnapshot
{
    public string version = "guidance-state-v1";
    public string phase, previousPhase, transitionReason, allowedAction;
    public long revision, observationSequence = -1;
    public double enteredAtSeconds, capturedAtSeconds;
    public bool gazeValid, controllerValid;
    public string gazeHitObjectId, vicinityWinnerObjectId, controllerHitObjectId, targetObjectId;
}
