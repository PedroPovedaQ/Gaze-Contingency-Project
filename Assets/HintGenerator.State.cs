using UnityEngine;

/// <summary>State and telemetry adapter for the same HintGenerator component; the pure machine owns transitions and qualification.</summary>
public partial class HintGenerator
{
    GuidanceDecision Decision(string stage, string reason, string category, string phrase,
        bool valid, bool inside, bool far, bool stable, string id = null)
    {
        var decision = GuidanceDecision.Capture(id ?? System.Guid.NewGuid().ToString("N"), stage, reason,
            category ?? "none", phrase, m_EvaluatedSample, Time.realtimeSinceStartupAsDouble,
            valid, inside, far, stable);
        decision.guidanceState = CurrentStateSnapshot;
        decision.targetGeometryAvailable = valid && m_GeometryAvailable;
        decision.turnRemainingDegrees = valid && !inside ? m_TurnRemaining : 0;
        decision.recentTargetVisitReceiptTime = m_State.LastTargetVisitTime;
        decision.recentTargetVisitObservationSequence = m_State.LastTargetVisitSequence;
        if (decision.targetGeometryAvailable) { decision.targetYawError = m_YawError; decision.targetPitchError = m_PitchError; }
        decision.verticalGuidanceMeasured = valid && m_VerticalMeasured;
        if (decision.verticalGuidanceMeasured)
        {
            decision.verticalErrorMetres = m_VerticalError;
            decision.guidancePlaneHeightMetres = m_GameManager.SearchPlaneHeight;
            decision.verticalErrorPlaneFraction = m_VerticalFraction;
        }
        decision.gazeOnTarget = m_GazeOnTarget;
        decision.targetVicinityMeasured = m_VicinityMeasured;
        decision.targetVicinityErrorDegrees = m_VicinityError; decision.targetVicinityRadiusDegrees = m_VicinityRadius;
        decision.gazeVicinityWinnerObjectId = m_ClosestVicinityObject != null
            ? m_ClosestVicinityObject.GetComponent<SpawnableObjectInfo>()?.objectId ?? "" : "";
        decision.gazeVicinityCandidateCount = m_VicinityCandidateCount;
        decision.gazeVicinityAmbiguous = m_VicinityAmbiguous;
        decision.gazeVicinityClosestErrorDegrees = m_ClosestVicinityError;
        decision.targetVicinityPaddingDegrees = m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees;
        decision.devVicinityRingEnabled = m_Vicinity != null && m_Vicinity.RingVisible;
        decision.controllerEvidenceValid = m_ControllerEvidenceValid; decision.controllerOnTarget = m_ControllerOnTarget;
        decision.visualReferenceObjectId = m_VisualReferenceObjectId;
        decision.visualReferenceVerticalDeltaMetres = m_VisualReferenceVerticalDelta;
        decision.visualReferencePitchDeltaDegrees = m_VisualReferencePitchDelta;
        decision.visualReferenceVerticalSuppressed = m_VisualReferenceVerticalSuppressed;
        decision.visualReferenceSuppressionReason = m_VisualReferenceSuppressionReason;
        decision.targetObjectId = m_EvaluatedTarget != null ? m_EvaluatedTarget.objectId : "";
        if (m_Controller != null)
        {
            decision.controllerTelemetryFrame = m_Controller.TelemetryFrame;
            decision.controllerTelemetryTime = m_Controller.TelemetryTime;
            var controllerTarget = m_Controller.RightTelemetry.target;
            decision.controllerTargetObjectId = controllerTarget != null ? controllerTarget.GetComponent<SpawnableObjectInfo>()?.objectId ?? "" : "";
        }
        return decision;
    }

    void UpdateSpeechEvidence(bool valid, bool inside, bool looking)
    {
        var e = m_Voice.GuidanceEvidence;
        double now = Time.realtimeSinceStartupAsDouble;
        e.guidanceState = CurrentStateSnapshot;
        e.snapshotTimeSeconds = now; e.phase = m_State.Phase.ToString(); e.valid = valid; e.insideTargetWall = valid && inside;
        e.wallEntryQualified = valid && inside && m_State.WallStable;
        e.gazeWithinTargetVicinity = valid && looking;
        e.targetGazeQualified = valid && looking && m_State.LookingStable;
        e.gazeRayHit = valid ? m_GazeRayHit : "unavailable";
        e.controllerEvidenceValid = m_ControllerEvidenceValid;
        e.controllerOnTarget = m_ControllerEvidenceValid && m_ControllerOnTarget;
        e.controllerTargetHoldQualified = e.controllerOnTarget && m_State.PointingStable;
        var hit = m_Controller != null ? m_Controller.RightTelemetry.target : null;
        e.controllerRayHit = !m_ControllerEvidenceValid ? "unavailable" : e.controllerOnTarget ? "target" :
            hit == null ? "none" : hit.GetComponentInParent<SpawnableObjectInfo>() != null ? "distractor" : "non_object";
        e.controllerTriggerHeld = m_ControllerEvidenceValid ? m_Controller.RightTelemetry.triggerHeld : -1;
        e.controllerAgeSeconds = m_Controller == null || m_Controller.TelemetryFrame < 0 ? -1 : System.Math.Max(0, now - m_Controller.TelemetryTime);
        e.gazeAgeSeconds = m_EvaluatedSample == null ? -1 : System.Math.Max(0, now - m_EvaluatedSample.inputEventTime);
        e.gazeReceiptAgeSeconds = m_EvaluatedSample == null ? -1 : System.Math.Max(0, now - m_EvaluatedSample.receiptTime);
        e.geometryAvailable = valid && m_GeometryAvailable;
        e.yawErrorDegrees = m_YawError; e.pitchErrorDegrees = m_PitchError;
        e.verticalMeasured = valid && m_VerticalMeasured; e.verticalPlaneFraction = m_VerticalFraction;
        e.vicinityMeasured = valid && m_VicinityMeasured;
        e.vicinityErrorDegrees = m_VicinityError; e.vicinityRadiusDegrees = m_VicinityRadius;
        e.vicinityCandidateCount = m_VicinityCandidateCount; e.vicinityAmbiguous = m_VicinityAmbiguous;
        e.visualReferenceObjectId = m_VisualReferenceObjectId;
        e.visualReferenceVerticalDeltaMetres = m_VisualReferenceVerticalDelta;
        e.visualReferencePitchDeltaDegrees = m_VisualReferencePitchDelta;
        e.visualReferenceVerticalSuppressed = m_VisualReferenceVerticalSuppressed;
        e.visualReferenceSuppressionReason = m_VisualReferenceSuppressionReason;
        e.targetAcknowledged = m_State.TargetAcknowledged;
        e.targetReturnPending = valid && m_State.RevisitPending(Time.realtimeSinceStartupAsDouble);
        e.lastTargetSeenAgeSeconds = m_State.LastTargetVisitTime < 0 ? -1 : System.Math.Max(0, now - m_State.LastTargetVisitTime);
        e.previousDirection = m_LastDirection; e.continuationUsed = m_ContinuationUsed;
    }

    public void SetLifecycle(GuidancePhase phase, string reason)
    {
        m_State.SetLifecycle(phase, reason, Time.realtimeSinceStartupAsDouble);
        m_GazeHitObjectId = null; m_ClosestVicinityObject = null; m_EvaluatedTarget = null;
        PublishState(false);
    }

    void Commit(GuidanceAction action, bool inside)
    {
        m_State.Commit(action, Time.realtimeSinceStartupAsDouble, inside, m_ControllerOnTarget,
            m_State.Candidate, m_TurnRemaining);
        // The action is consumed against the frame that authorized it. Do not mix
        // callback geometry reads with that frame's recorded gaze/controller IDs.
        var captured = CurrentStateSnapshot;
        if (captured != null)
        {
            CurrentStateSnapshot = m_State.Snapshot(Time.realtimeSinceStartupAsDouble, captured.observationSequence,
                captured.gazeValid, captured.controllerValid, captured.gazeHitObjectId,
                captured.vicinityWinnerObjectId, captured.controllerHitObjectId, captured.targetObjectId);
            CurrentStateSnapshot.guidancePolicy = SessionConfig.GuidancePolicy;
            if (m_Voice != null) m_Voice.GuidanceEvidence.guidanceState = CurrentStateSnapshot;
        }
    }

    void PublishState(bool valid)
    {
        bool searching = m_State.Phase != GuidancePhase.Opening && m_State.Phase != GuidancePhase.Inactive &&
            m_State.Phase != GuidancePhase.Paused && m_State.Phase != GuidancePhase.Completed && m_State.Phase != GuidancePhase.Stopped && m_State.Phase != GuidancePhase.TimedOut;
        var controllerHit = m_Controller != null ? m_Controller.RightTelemetry.target : null;
        CurrentStateSnapshot = m_State.Snapshot(Time.realtimeSinceStartupAsDouble, m_EvaluatedSample?.sequence ?? -1,
            valid, searching && m_ControllerEvidenceValid, m_GazeHitObjectId,
            m_ClosestVicinityObject?.GetComponent<SpawnableObjectInfo>()?.objectId,
            controllerHit?.GetComponent<SpawnableObjectInfo>()?.objectId, m_EvaluatedTarget?.objectId);
        CurrentStateSnapshot.guidancePolicy = SessionConfig.GuidancePolicy;
        if (m_PublishedRevision != m_State.Revision)
        {
            m_PublishedRevision = m_State.Revision;
            m_Voice?.CancelPendingGuidance(); m_Navigation?.Cancel();
            m_Voice?.RecordGuidanceState(CurrentStateSnapshot);
        }
        if (m_Voice != null)
        {
            m_Voice.GuidanceEvidence.guidanceState = CurrentStateSnapshot;
            m_Voice.GuidanceEvidence.phase = m_State.Phase.ToString();
            if (!searching) m_Voice.GuidanceEvidence.valid = false;
        }
    }
}
