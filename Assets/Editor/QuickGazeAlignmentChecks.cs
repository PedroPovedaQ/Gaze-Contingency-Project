using System;
using UnityEditor;
using UnityEngine;

public static class QuickGazeAlignmentChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    [MenuItem("Tools/Codex/Check Quick Eye Alignment")]
    public static void Run()
    {
        var alignment = new GazeQuickAlignment();
        double time = 10;
        alignment.Begin(new Vector3(1, 1.6f, -2), Quaternion.Euler(0, 65, 0), time);
        FeedPoint(alignment, ref time, 2, 1);
        Check(alignment.IsRunning && !alignment.IsActive && alignment.TargetIndex == 1,
            "A fit alone must never activate correction");
        FeedPoint(alignment, ref time, 2, 1);
        Check(!alignment.IsActive && alignment.TargetIndex == 2, "Both held-out points are required");
        FeedPoint(alignment, ref time, 2, 1);
        Check(alignment.IsActive && alignment.Report.outcome == "passed", "Constant bias passes independent checks");
        Check(Mathf.Abs(alignment.Report.yawCorrectionDegrees + 2) < .01f &&
            Mathf.Abs(alignment.Report.pitchCorrectionDegrees + 1) < .01f, "Opposite angular bias is fitted");
        var sample = Sample(time + 1, new Vector3(3, 1.8f, 4), Quaternion.Euler(-10, -110, 6), new Vector3(1, 2, -2), 2, 1);
        var rawOrigin = sample.rawWorldOrigin; var rawDirection = sample.rawWorldDirection;
        var expected = (new Vector3(1, 2, -2) - sample.worldOrigin).normalized;
        alignment.Apply(sample);
        Check(sample.alignmentApplied && Vector3.Angle(sample.worldDirection, expected) < .03f,
            "Correction follows head rotation and works at another distance");
        Check(sample.rawWorldOrigin == rawOrigin && sample.rawWorldDirection == rawDirection && sample.worldOrigin == rawOrigin,
            "Raw gaze and the eye origin are never moved");
        var changed = Sample(time + 2, Vector3.zero, Quaternion.identity, Vector3.forward * 2, 2, 1);
        changed.raySource = "HTC_binocular_mean";
        alignment.Apply(changed);
        Check(!alignment.IsActive && !changed.alignmentApplied, "Changing gaze source invalidates the old correction");

        alignment.Begin(Vector3.zero, Quaternion.identity, time += 10);
        FeedPoint(alignment, ref time, 2, 1);
        FeedPoint(alignment, ref time, -2, -1);
        Check(!alignment.IsActive && !alignment.IsRunning && alignment.Report.outcome == "rejected",
            "Reject correction that worsens a separate target");

        alignment.Begin(Vector3.zero, Quaternion.identity, time += 10);
        for (int i = 0; i < 100; i++)
        {
            var duplicate = Sample(time + i * .02, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, 2, 1);
            duplicate.inputEventTime = time; // Polls must not masquerade as distinct deliveries.
            alignment.Observe(duplicate);
        }
        Check(alignment.TargetIndex == 0 && !alignment.IsActive, "Repeated timestamps cannot complete a point");
        var invalid = Sample(time + 2.1, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, 2, 1);
        invalid.valid = false; alignment.Observe(invalid);
        Check(alignment.PointProgress == 0, "Invalid tracking clears the collection window");
        var timeout = Sample(time + 21, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, 2, 1);
        alignment.Observe(timeout);
        Check(!alignment.IsRunning && alignment.Report.outcome == "rejected", "No infinite wait for usable gaze");

        alignment.Begin(Vector3.zero, Quaternion.identity, time += 30);
        FeedPoint(alignment, ref time, 9, 0);
        Check(!alignment.IsActive && alignment.Report.outcome == "rejected", "Large bias requires vendor recalibration");
        alignment.Begin(Vector3.zero, Quaternion.identity, time += 10);
        FeedPoint(alignment, ref time, 0, 0);
        FeedPoint(alignment, ref time, 0, 0);
        FeedPoint(alignment, ref time, 0, 0);
        Check(alignment.IsActive, "Already-aligned gaze can pass without inventing a bias");
        var lostHead = Sample(time + 1, Vector3.zero, Quaternion.identity, Vector3.forward * 2, 0, 0);
        lostHead.headValid = false; alignment.Apply(lostHead);
        Check(!lostHead.valid && !lostHead.alignmentApplied && alignment.IsActive,
            "Missing head pose withholds corrected gaze without substituting uncorrected guidance");
        alignment.CancelOrClear("Headset focus lost", time);
        Check(!alignment.IsActive, "Focus/refit invalidation clears alignment");

        alignment.Begin(Vector3.zero, Quaternion.identity, time += 10);
        for (int i = 0; i < 150 && alignment.IsRunning; i++)
        {
            time += .02;
            alignment.Observe(Sample(time, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, i % 2 == 0 ? -3 : 3, 0));
        }
        Check(alignment.Report.outcome == "rejected", "Unstable gaze cannot be repaired with a constant offset");
        alignment.Begin(Vector3.zero, Quaternion.identity, time += 10);
        FeedPoint(alignment, ref time, 2, 1, "HTC_binocular_mean");
        FeedPoint(alignment, ref time, 2, 1, "HTC_binocular_mean");
        FeedPoint(alignment, ref time, 2, 1, "HTC_binocular_mean");
        Check(alignment.IsActive && alignment.Report.raySource == "HTC_binocular_mean",
            "Fit and validate actual native fallback without using rejected EXT geometry");
        string json = JsonUtility.ToJson(alignment.Report);
        var saved = JsonUtility.FromJson<GazeQuickAlignment.AlignmentReport>(json);
        Check(saved.points.Count == 3 && saved.points[1].purpose == "held_out" &&
            saved.points[2].afterMedianDegrees < .03f && saved.minimumInputUpdates == 30,
            "Persist source, independent results, sample identities and pilot limits");
        var newParticipant = Sample(time + 1, Vector3.zero, Quaternion.identity, Vector3.forward * 2, 2, 1);
        newParticipant.raySource = "HTC_binocular_mean"; newParticipant.participant = "another-person";
        alignment.Apply(newParticipant);
        Check(!alignment.IsActive && !newParticipant.alignmentApplied, "Participant changes cannot reuse an offset");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        for (int dot = 0; dot < 3; dot++)
        {
            FeedPoint(alignment, ref time, dot + 1, 1);
            Check(!alignment.IsActive, "Timed fit dots never directly enable a correction");
        }
        Check(Mathf.Abs(alignment.Report.yawCorrectionDegrees + 2) < .01f &&
            Mathf.Abs(alignment.Report.pitchCorrectionDegrees + 1) < .01f,
            "Guided entry averages all three dot mean offsets equally");
        Check(alignment.Report.assumedFixation && alignment.TargetIndex == 3, "Instruction-based fixation assumption is documented");
        Check(!alignment.ConfirmGuided(time), "Cannot accept before independent shapes");
        for (int shape = 0; shape < 3; shape++) FeedPoint(alignment, ref time, 2, 1);
        Check(alignment.AwaitingConfirmation && !alignment.IsActive && alignment.Report.points.Count == 6,
            "Three independent shapes leave correction pending explicit OK");
        var preview = Sample(time, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, 2, 1);
        preview.unalignedWorldDirection = preview.worldDirection;
        Check(Vector3.Angle(alignment.PreviewDirection(preview), alignment.CurrentTarget.normalized) < .03f,
            "Candidate preview accurately illuminates independent shapes");
        var unmodified = preview.worldDirection; alignment.Apply(preview);
        Check(preview.worldDirection == unmodified && !preview.alignmentApplied, "Pending fit does not alter guidance or recorded used ray");
        Check(alignment.ConfirmGuided(time) && alignment.Report.userConfirmed && alignment.IsActive,
            "Only explicit acceptance activates the checked correction");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        for (int dot = 0; dot < 3; dot++) FeedPoint(alignment, ref time, 2, 1);
        FeedPoint(alignment, ref time, -2, -1);
        Check(!alignment.IsActive && alignment.Report.outcome == "rejected", "Incorrect independent shape cannot pass");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        // Regression fixture derived from the three recorded dot means in the rejected
        // a4e004... headset attempt. This tests the algorithm, not hardware accuracy.
        FeedPoint(alignment, ref time, .902891f, 4.102316f);
        FeedPoint(alignment, ref time, -.060690f, 4.789500f);
        FeedPoint(alignment, ref time, 1.812642f, 4.954318f);
        Check(alignment.IsRunning && alignment.TargetIndex == 3 && !alignment.IsActive,
            "A dot above five degrees must not reject an acceptable three-dot mean");
        Check(new Vector2(alignment.Report.yawCorrectionDegrees, alignment.Report.pitchCorrectionDegrees).magnitude < 5,
            "Recorded three-dot mean remains within the correction cap");
        for (int shape = 0; shape < 3; shape++) FeedPoint(alignment, ref time, .884948f, 4.615378f);
        Check(alignment.AwaitingConfirmation && !alignment.IsActive, "Mean fit still requires independent checks and explicit OK");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        for (int dot = 0; dot < 3; dot++) FeedPoint(alignment, ref time, 6, 0);
        Check(alignment.IsRunning && alignment.TargetIndex == 3 && !alignment.IsActive &&
            !alignment.ConfirmGuided(time), "Six-degree fit is permitted but cannot bypass held-out checks");
        for (int shape = 0; shape < 3; shape++) FeedPoint(alignment, ref time, 6, 0);
        Check(alignment.AwaitingConfirmation && alignment.ConfirmGuided(time), "Six-degree correction requires independent checks and confirmation");
        Check(alignment.Report.maxCorrectionDegrees == 8 && Mathf.Abs(alignment.Report.yawCorrectionDegrees + 6) < .01f,
            "Record the raised cap and measured correction, not a hardcoded six-degree offset");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        for (int dot = 0; dot < 3; dot++) FeedPoint(alignment, ref time, 0, 6);
        FeedPoint(alignment, ref time, 0, 2);
        Check(alignment.Report.outcome == "rejected" && !alignment.IsActive,
            "Raised cap still rejects a vertical correction that fails held-out accuracy");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        for (int dot = 0; dot < 3; dot++) FeedPoint(alignment, ref time, 9, 0);
        Check(alignment.Report.points.Count == 3 && alignment.Report.outcome == "rejected" && !alignment.IsActive &&
            !alignment.ConfirmGuided(time), "Oversized completed mean still rejects without applying an offset");
        alignment.BeginGuided(Vector3.zero, Quaternion.identity, time += 10);
        var countdownStart = time;
        for (int n = 0; n < 120; n++)
        { time += .02; alignment.Observe(Sample(time, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, 2, 1)); }
        Check(alignment.PointProgress == 0 && alignment.CountdownRemaining(time) > 0, "No samples collected during 3-2-1");
        alignment.CancelOrClear("Skip", time);
        Check(!alignment.IsActive && !alignment.AwaitingConfirmation, "Skip cancels guided entry");
        alignment.BeginGuided(Vector3.zero,Quaternion.identity,time+=10,studyVerification:true);
        for(int dot=0;dot<3;dot++)FeedPoint(alignment,ref time,2,1);
        Check(alignment.AwaitingAccuracy && !alignment.IsActive && !alignment.IsRunning &&
            !alignment.AwaitingConfirmation && alignment.Report.points.Count==3,
            "Simplified study fit stops at three dots with no shapes or accepted correction");
        var candidate=Sample(time+1,Vector3.zero,Quaternion.identity,Vector3.forward*2,2,1);
        alignment.Apply(candidate);
        Check(candidate.alignmentApplied && candidate.alignmentStatus=="awaiting_accuracy" && !alignment.IsActive,
            "Candidate is measurable but not accepted for study trials");
        Check(!alignment.ConfirmGuided(time) && !alignment.ConfirmStudyAccuracy(new StudyGazeAccuracy.Result {passed=false},"check",time),
            "Neither old OK nor a failed five-dot check can accept the candidate");
        Check(alignment.ConfirmStudyAccuracy(new StudyGazeAccuracy.Result {passed=true},"check",time) &&
            alignment.IsActive && !alignment.AwaitingAccuracy && !alignment.Report.userConfirmed && alignment.Report.accuracyValidationId=="check",
            "Passing independent verification is linked without inventing shape confirmation");
        alignment.BeginGuided(Vector3.zero,Quaternion.identity,time+=10,studyVerification:true);
        for(int dot=0;dot<3;dot++)FeedPoint(alignment,ref time,2,1);
        alignment.CancelOrClear("Skip",time);
        Check(!alignment.IsActive && !alignment.AwaitingAccuracy && !alignment.ConfirmStudyAccuracy(new StudyGazeAccuracy.Result {passed=true},"check",time),
            "Skip clears the temporary correction and prevents later acceptance");
        Debug.Log("[QuickGazeAlignmentChecks] PASS: independent checks, head-relative bias, immutable raw gaze, source change, gaps, duplicates, timeout, large bias and zero bias. Hardware validation remains required.");
    }

    static void FeedPoint(GazeQuickAlignment alignment, ref double time, float yaw, float pitch, string source = "OpenXR_EXT")
    {
        int index = alignment.TargetIndex;
        // Fixed head; targets stay at their recorded world positions.
        for (int i = 0; i < 400 && alignment.IsRunning && alignment.TargetIndex == index; i++)
        {
            time += .02;
            var sample = Sample(time, Vector3.zero, Quaternion.identity, alignment.CurrentTarget, yaw, pitch);
            sample.raySource = source; alignment.Observe(sample);
        }
    }

    static GazeObservation Sample(double time, Vector3 origin, Quaternion head, Vector3 target, float yaw, float pitch)
    {
        var expected = Quaternion.Inverse(head) * (target - origin).normalized;
        // Independent fixture construction: measured gaze is right/up of the target in head space.
        float az = Mathf.Atan2(expected.x, expected.z) + yaw * Mathf.Deg2Rad;
        float el = Mathf.Asin(expected.y) + pitch * Mathf.Deg2Rad;
        var direction = head * new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
        return new GazeObservation { valid = true, headValid = true, focused = true, newInputUpdate = true,
            inputEventTime = time, receiptTime = time, participant = "alignment-test", deviceId = 1,
            worldOrigin = origin, worldDirection = direction, rawWorldOrigin = origin, rawWorldDirection = direction,
            headRotation = head, headPosition = origin, sequence = (long)(time * 1000), recordingId = "fixture" };
    }
}
