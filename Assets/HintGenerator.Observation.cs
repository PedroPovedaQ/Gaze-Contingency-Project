using UnityEngine;

/// <summary>Unity observation adapter for the HintGenerator component; callbacks read current geometry without advancing state gates.</summary>
public partial class HintGenerator
{
    string m_VisualReferenceObjectId, m_VisualReferenceSuppressionReason;
    float m_VisualReferenceVerticalDelta, m_VisualReferencePitchDelta;
    bool m_VisualReferenceVerticalSuppressed;

    public bool CaptureTimeoutSelectionReminder(bool deadlineReached)
    {
        if (!deadlineReached || !Active()) return false;
        Evaluate(out bool valid, out bool inside, out bool far);
        bool pointing = EvaluatePointing(valid);
        bool looking = EvaluateLooking(valid);
        bool remind = m_State.ShouldRemindOnTimeout(deadlineReached, looking, pointing,
            m_ControllerEvidenceValid && m_Controller.RightTelemetry.triggerHeld == 0);
        Record(Decision("timeout_evidence", remind ? "target_held_without_selection_at_deadline" : "ordinary_timeout",
            "trial_timeout", "", valid, inside, far, m_State.PointingStable));
        return remind;
    }

    bool EvaluateLooking(bool valid)
    {
        m_GazeOnTarget = false; m_GazeRayHit = "unavailable"; m_GazeHitObjectId = null;
        if (valid && m_EvaluatedTarget != null && m_EvaluatedSample != null)
        {
            m_GazeRayHit = "none";
            if (Physics.Raycast(m_EvaluatedSample.worldOrigin, m_EvaluatedSample.worldDirection,
                out var hit, 30, 1 << 8, QueryTriggerInteraction.Ignore))
            {
                var info = hit.collider.GetComponentInParent<SpawnableObjectInfo>();
                m_GazeHitObjectId = info?.objectId;
                m_GazeOnTarget = info == m_EvaluatedTarget;
                m_GazeRayHit = m_GazeOnTarget ? "target" : info != null ? "distractor" : "non_object";
            }
        }
        return valid && m_EvaluatedTarget != null && m_ClosestVicinityObject == m_EvaluatedTarget.gameObject;
    }

    bool EvaluatePointing(bool valid)
    {
        RefreshControllerEvidence();
        // Evaluate already measured this observation for visual-reference
        // consistency; reuse its winner instead of rescanning all objects.
        if (valid && m_VicinityMeasured)
            return m_ControllerOnTarget && m_ClosestVicinityObject == m_EvaluatedTarget.gameObject;
        m_VicinityMeasured = false;
        m_VicinityError = m_VicinityRadius = 0;
        m_ClosestVicinityObject = null; m_VicinityCandidateCount = 0;
        m_VicinityAmbiguous = false; m_ClosestVicinityError = 0;
        if (!valid || m_EvaluatedTarget == null || m_EvaluatedSample == null)
        { m_Vicinity?.Hide(); return false; }
        var collider = m_EvaluatedTarget.GetComponent<Collider>();
        if (collider == null || !collider.enabled) { m_Vicinity?.Hide(); return false; }
        m_VicinityMeasured = GazeTargetVicinity.Measure(m_EvaluatedSample.worldOrigin,
            m_EvaluatedSample.worldDirection, collider.bounds,
            m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees,
            out m_VicinityError, out m_VicinityRadius);
        if (!m_VicinityMeasured) { m_Vicinity?.Hide(); return false; }
        // Record the winning ring even when it belongs to a distractor; the
        // target remains eligible only when it is the unique closest object.
        m_ClosestVicinityObject = GazeTargetVicinity.ClosestObject(m_EvaluatedSample.worldOrigin,
                m_EvaluatedSample.worldDirection, m_GameManager?.SpawnedObjects,
                m_Vicinity != null ? m_Vicinity.PaddingDegrees : GazeTargetVicinity.DefaultPaddingDegrees,
                out m_VicinityCandidateCount, out m_VicinityAmbiguous, out m_ClosestVicinityError);
        bool targetClosest = m_ClosestVicinityObject == m_EvaluatedTarget.gameObject;
        m_Vicinity?.Draw(m_EvaluatedSample.worldOrigin, collider.bounds, m_VicinityRadius, targetClosest);
        return m_ControllerOnTarget && targetClosest;
    }

    // Controller-only refresh is cheap enough for asynchronous final playback
    // guards; it does not redo the O(objects) gaze-vicinity winner scan.
    void RefreshControllerEvidence()
    {
        m_ControllerEvidenceValid = m_ControllerOnTarget = false;
        if (m_EvaluatedTarget != null)
        {
            if (m_Controller == null) m_Controller = GetComponent<ControllerRaySelector>();
            if (m_Controller != null)
            {
                m_ControllerEvidenceValid = GazeTargetVicinity.ControllerCurrent(m_Controller.RightTelemetry,
                    m_Controller.TelemetryFrame, m_Controller.TelemetryTime, Time.frameCount, Time.realtimeSinceStartupAsDouble);
                m_ControllerOnTarget = m_ControllerEvidenceValid && m_Controller.RightTelemetry.target == m_EvaluatedTarget.gameObject;
            }
        }
    }

    void PreviewPausedVicinity()
    {
        if (!SessionConfig.DevelopmentQuickTest || m_GameManager == null || !m_GameManager.ResearcherPaused ||
            m_Vicinity == null || !m_Vicinity.RingVisible)
        { m_Vicinity?.Hide(); return; }
        // Development-only visual feedback while adjusting settings; no gate/history/speech update.
        Evaluate(out bool valid, out _, out _);
        EvaluatePointing(valid);
    }

    GazeTargetHistory.Sample? TargetHistorySample(bool valid)
    {
        if (!valid || !m_GeometryAvailable || m_EvaluatedSample == null) return null;
        return new GazeTargetHistory.Sample {
            time = m_EvaluatedSample.receiptTime, inputTime = m_EvaluatedSample.inputEventTime,
            observationSequence = m_EvaluatedSample.sequence, eyeRecordingId = m_EvaluatedSample.recordingId,
            deviceId = m_EvaluatedSample.deviceId, yawError = m_YawError, pitchError = m_PitchError
        };
    }

    string Evaluate(out bool valid,out bool inside,out bool far, bool advanceTurn = false)
    {
        m_GazeHitObjectId = null;
        m_VisualReferenceObjectId = m_VisualReferenceSuppressionReason = null;
        m_VisualReferenceVerticalDelta = m_VisualReferencePitchDelta = 0; m_VisualReferenceVerticalSuppressed = false;
        valid=inside=far=false; m_GazeOnTarget = false; m_GeometryAvailable = false; m_EvaluatedTarget = null;
        m_VerticalMeasured = false; m_VerticalError = 0; m_VerticalFraction = float.NaN;
        m_ClosestVicinityObject = null; m_VicinityCandidateCount = 0;
        m_VicinityAmbiguous = false; m_ClosestVicinityError = 0;
        m_VicinityMeasured = m_ControllerEvidenceValid = m_ControllerOnTarget = false;
        m_VicinityError = m_VicinityRadius = 0;
        m_EvaluatedSample = SharedGazeStream.Instance != null ? SharedGazeStream.Instance.Latest : null;
        if (m_GameManager == null || !m_GameManager.RotationalBetaEnabled || !TryGetTargetInfo(out var target)) return null;
        m_EvaluatedTarget = target;
        if (SharedGazeStream.Instance == null || !SharedGazeStream.Instance.TryGetValid(out var sample)) return null;
        m_EvaluatedSample = sample;
        Vector3 gaze=sample.worldDirection, delta=target.transform.position-sample.worldOrigin;
        // Azimuth becomes unstable looking nearly straight up/down. This is a gap, not wall departure.
        if (gaze.x*gaze.x+gaze.z*gaze.z<.04f || delta.sqrMagnitude<.001f) return null;
        inside=m_GameManager.IsGazeInSearchSector(sample.worldOrigin,gaze,target.planeId,out valid);
        if (!valid) return null;
        float gazeElevation=Mathf.Atan2(gaze.y,new Vector2(gaze.x,gaze.z).magnitude)*Mathf.Rad2Deg;
        float targetElevation=Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg;
        // Within the correct wall, combine elevation and horizontal error for diagonal refinement.
        // Measure the tie relative to the seated layout, independent of room yaw.
        Quaternion inverse = Quaternion.Euler(0, -m_GameManager.SeatedForwardYaw, 0);
        Vector3 localGaze = inverse * gaze, localDelta = inverse * delta;
        float yaw=DirectionalHintPolicy.SignedYaw(localGaze.x,localGaze.z,localDelta.x,localDelta.z);
        m_YawError = yaw; m_PitchError = targetElevation - gazeElevation; m_GeometryAvailable = true;
        string turn = advanceTurn ? m_State.Turn.Resolve(yaw, inside) : m_State.Turn.Peek(yaw, inside);
        m_TurnRemaining = m_State.Turn.Remaining(yaw);
        if (!inside) { far = m_TurnRemaining > 45; return turn; }
        m_VerticalMeasured = m_GameManager.TryVerticalGuidanceError(sample.worldOrigin, gaze, target.planeId,
            target.transform.position.y, out m_VerticalError);
        if (!m_VerticalMeasured) { valid = false; return null; }
        m_VerticalFraction = m_VerticalError / m_GameManager.SearchPlaneHeight;
        string direction = DirectionalHintPolicy.WithinArea(m_YawError, m_PitchError, m_State.Candidate);
        EvaluatePointing(valid); // Same unique padded-ring winner used for the visible shine.
        var reference = m_ClosestVicinityObject != null ? m_ClosestVicinityObject.GetComponent<SpawnableObjectInfo>() : null;
        bool usableReference = reference != null && reference != target && reference.planeId == target.planeId;
        if (usableReference)
        {
            m_VisualReferenceObjectId = reference.objectId;
            m_VisualReferenceVerticalDelta = target.transform.position.y - reference.transform.position.y;
            Vector3 referenceDelta = reference.transform.position - sample.worldOrigin;
            float referenceElevation = Mathf.Atan2(referenceDelta.y, new Vector2(referenceDelta.x, referenceDelta.z).magnitude) * Mathf.Rad2Deg;
            m_VisualReferencePitchDelta = targetElevation - referenceElevation;
        }
        direction = DirectionalHintPolicy.ConsistentWithVisibleReference(direction, usableReference,
            m_VisualReferencePitchDelta, out m_VisualReferenceVerticalSuppressed);
        if (m_VisualReferenceVerticalSuppressed)
            m_VisualReferenceSuppressionReason = "raw_vertical_conflicts_with_visible_distractor";
        return direction;
    }

    bool TryGetTargetInfo(out SpawnableObjectInfo info)
    {
        info=null;
        var target=m_GameManager.CurrentTarget;
        foreach(var obj in m_GameManager.SpawnedObjects)
        {
            if(obj==null || !obj.activeSelf) continue;
            var candidate=obj.GetComponent<SpawnableObjectInfo>();
            if(candidate!=null && candidate.shapeName==target.shape && candidate.colorName==target.color)
            { info=candidate; return true; }
        }
        return false;
    }
}
