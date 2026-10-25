using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short, head-relative angular alignment, not vendor calibration or a fixation detector.
/// Fit one world-fixed dot; independently check two others before enabling any correction.
/// All limits below are pilot settings requiring Focus Vision wearer validation.
/// </summary>
public sealed class GazeQuickAlignment
{
    public const float SettleSeconds = .35f, CollectionSeconds = 1.5f, MaxPointSeconds = 20;
    public const int MinimumUpdates = 30;
    public const float MaxCorrectionDegrees = 5, MaxSpreadDegrees = 1.5f;
    public const float MaxMedianErrorDegrees = 2, MaxP90ErrorDegrees = 3;
    public bool IsRunning { get; private set; }
    public bool IsActive { get; private set; }
    public const float GuidedAttentionCueSeconds = .4f;
    public bool GuidedEntry { get; private set; }
    public bool AwaitingConfirmation { get; private set; }
    public int FitPointCount => GuidedEntry ? 3 : 1;
    public int PointCount => GuidedEntry ? 6 : 3;
    public double CountdownRemaining(double now) => GuidedEntry ? Math.Max(0, m_SettleUntil - now) : 0;
    public Vector3 TargetAt(int index) => m_Targets[index];
    public int TargetIndex { get; private set; }
    public Vector3 CurrentTarget => m_Targets[TargetIndex];
    public string Status { get; private set; } = "No quick alignment applied.";
    public AlignmentReport Report { get; private set; }
    public float PointProgress => m_Window.Count < 2 ? 0 : Mathf.Clamp01((float)
        ((m_Window[m_Window.Count - 1].time - m_Window[0].time) / CollectionSeconds));

    readonly Vector3[] m_Targets = new Vector3[6];
    readonly List<Measurement> m_Window = new List<Measurement>();
    double m_PointStarted, m_SettleUntil, m_LastInput = -1, m_PreviousReceipt = -1;
    Quaternion m_WindowHead;
    bool m_SourceBound;
    string m_State = "none";

    public void Begin(Vector3 headPosition, Quaternion headRotation, double now)
    {
        GuidedEntry = AwaitingConfirmation = false;
        IsActive = false; IsRunning = true; TargetIndex = 0; m_SourceBound = false;
        m_LastInput = m_PreviousReceipt = -1;
        Report = new AlignmentReport { id = Guid.NewGuid().ToString("N"), startedAt = now,
            utcStarted = DateTime.UtcNow.ToString("o") };
        m_Targets[0] = headPosition + headRotation * Vector3.forward * 2;
        m_Targets[1] = headPosition + headRotation * Quaternion.Euler(-6, 12, 0) * Vector3.forward * 2;
        m_Targets[2] = headPosition + headRotation * Quaternion.Euler(6, -12, 0) * Vector3.forward * 2;
        StartPoint(now);
    }

    // Three timed fit locations; fixation is instructed, not selected by measured proximity.
    public void BeginGuided(Vector3 position, Quaternion rotation, double now)
    {
        Begin(position, rotation, now);
        GuidedEntry = true;
        Report.method = "head_local_three_dot_mean_user_confirmed_v2";
        Report.assumedFixation = true;
        Report.fitPointCount = 3;
        Report.settleSeconds = 3;
        Report.attentionCueSeconds = GuidedAttentionCueSeconds;
        Report.scope = "pilot_countdown_assumed_fixation_three_fit_three_check_not_vendor_calibration";
        m_Targets[3] = position + rotation * Quaternion.Euler(5, 7, 0) * Vector3.forward * 2;
        m_Targets[4] = position + rotation * Quaternion.Euler(-7, -7, 0) * Vector3.forward * 2;
        m_Targets[5] = position + rotation * Quaternion.Euler(-8, 0, 0) * Vector3.forward * 2;
        StartPoint(now);
    }

    public Vector3 PreviewDirection(GazeObservation sample) => sample.headRotation * CorrectHeadDirection(
        Quaternion.Inverse(sample.headRotation) * sample.unalignedWorldDirection,
        Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees);

    public bool ConfirmGuided(double now)
    {
        if (!AwaitingConfirmation || !GuidedEntry) return false;
        AwaitingConfirmation = false; IsActive = true; m_State = "passed";
        Report.userConfirmed = true; Report.outcome = "passed"; Report.finishedAt = now;
        Status = Report.message = "Three-dot mean checked on three shapes and confirmed by the user.";
        return true;
    }

    void StartPoint(double now)
    {
        m_PointStarted = now; ResetWindow(now);
        m_State = TargetIndex < FitPointCount ? "collecting_fit" : "checking_independent_target";
        Status = GuidedEntry ? $"Look at {(TargetIndex < 3 ? "dot" : "shape")} {TargetIndex % 3 + 1} of 3 and hold your head still."
            : TargetIndex == 0 ? "Look directly at the dot and hold your head still."
            : $"Check {TargetIndex} of 2: look at the new dot. Keep your head still.";
    }

    void ResetWindow(double now)
    {
        m_Window.Clear(); m_SettleUntil = now + (GuidedEntry ? 3 : SettleSeconds);
    }

    public void Observe(GazeObservation sample)
    {
        if (!IsRunning) return;
        double now = sample.receiptTime;
        if (now - m_PointStarted > MaxPointSeconds)
        { Reject("Could not collect stable gaze. Refit, run HTC calibration, then retry.", now); return; }
        if (!sample.valid || !sample.headValid || !sample.focused || !Finite(sample.headRotation) ||
            !Finite(sample.worldOrigin) || !Finite(sample.worldDirection) || sample.worldDirection.sqrMagnitude < .5f)
        {
            ResetWindow(now); Status = "Waiting for valid eyes and head tracking. Look at the dot."; return;
        }
        if (!m_SourceBound)
        {
            Report.participant = sample.participant; Report.deviceId = sample.deviceId;
            Report.raySource = sample.raySource; Report.baseCorrection = sample.correction;
            Report.recordingId = sample.recordingId; m_SourceBound = true;
        }
        else if (!SameSource(sample))
        { Reject("Participant or gaze source changed. Start alignment again.", now); return; }
        // These are distinct software deliveries, not a claim of native sensor rate.
        if (!sample.newInputUpdate || sample.inputEventTime <= m_LastInput) return;
        m_LastInput = sample.inputEventTime;
        if (m_PreviousReceipt >= 0 && now - m_PreviousReceipt > .1) ResetWindow(now);
        m_PreviousReceipt = now;
        if (now < m_SettleUntil) return;
        if (m_Window.Count == 0) m_WindowHead = sample.headRotation;
        if (Quaternion.Angle(m_WindowHead, sample.headRotation) > 5)
        { ResetWindow(now); Status = "Keep your head still and look at the dot. Restarting this dot."; return; }
        var inverseHead = Quaternion.Inverse(sample.headRotation);
        var measured = (inverseHead * sample.worldDirection).normalized;
        var expected = (inverseHead * (CurrentTarget - sample.worldOrigin)).normalized;
        if (!Finite(expected) || expected.sqrMagnitude < .5f) { ResetWindow(now); return; }
        Vector2 actualAngles = Angles(measured), wantedAngles = Angles(expected);
        m_Window.Add(new Measurement { time = sample.inputEventTime, sequence = sample.sequence,
            measured = measured, expected = expected,
            yaw = Mathf.DeltaAngle(actualAngles.x, wantedAngles.x), pitch = wantedAngles.y - actualAngles.y });
        Status = $"Keep looking at the dot — {PointProgress:P0}";
        if (PointProgress < 1 || m_Window.Count < MinimumUpdates) return;
        FinishPoint(now);
    }

    void FinishPoint(double now)
    {
        float yaw = Percentile(m_Window.ConvertAll(x => x.yaw), .5f);
        float pitch = Percentile(m_Window.ConvertAll(x => x.pitch), .5f);
        float spread = Percentile(m_Window.ConvertAll(x => new Vector2(Mathf.DeltaAngle(yaw, x.yaw), x.pitch - pitch).magnitude), .9f);
        if (GuidedEntry && TargetIndex < FitPointCount)
        {
            yaw = Mean(m_Window.ConvertAll(x => x.yaw));
            pitch = Mean(m_Window.ConvertAll(x => x.pitch));
        }
        if (!GuidedEntry && TargetIndex == 0)
        {
            Report.yawCorrectionDegrees = yaw; Report.pitchCorrectionDegrees = pitch;
            // Do not fit sub-quarter-degree noise into otherwise aligned gaze.
            if (new Vector2(yaw, pitch).magnitude < .25f)
                Report.yawCorrectionDegrees = Report.pitchCorrectionDegrees = 0;
        }
        var raw = m_Window.ConvertAll(x => Vector3.Angle(x.measured, x.expected));
        var corrected = m_Window.ConvertAll(x => Vector3.Angle(CorrectHeadDirection(x.measured,
            Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees), x.expected));
        var point = new AlignmentPoint { index = TargetIndex, purpose = TargetIndex < FitPointCount ? "fit" : "held_out",
            meanYawOffsetDegrees = Mean(m_Window.ConvertAll(x => x.yaw)),
            meanPitchOffsetDegrees = Mean(m_Window.ConvertAll(x => x.pitch)), assumedFixation = GuidedEntry,
            targetWorld = CurrentTarget, updates = m_Window.Count, firstSequence = m_Window[0].sequence,
            lastSequence = m_Window[m_Window.Count - 1].sequence, firstInputTime = m_Window[0].time,
            lastInputTime = m_Window[m_Window.Count - 1].time, spreadP90Degrees = spread,
            beforeMedianDegrees = Percentile(raw, .5f), beforeP90Degrees = Percentile(raw, .9f),
            afterMedianDegrees = Percentile(corrected, .5f), afterP90Degrees = Percentile(corrected, .9f) };
        Report.points.Add(point);
        if (spread > MaxSpreadDegrees)
        { Reject("Gaze was too variable. Refit and retry; no correction applied.", now); return; }
        // Guided entry fits one equal-weight mean across three dots, not three
        // independent corrections. Apply the correction cap to that completed fit.
        if (GuidedEntry && TargetIndex == 2)
        {
            Report.yawCorrectionDegrees = (Report.points[0].meanYawOffsetDegrees + Report.points[1].meanYawOffsetDegrees + Report.points[2].meanYawOffsetDegrees) / 3;
            Report.pitchCorrectionDegrees = (Report.points[0].meanPitchOffsetDegrees + Report.points[1].meanPitchOffsetDegrees + Report.points[2].meanPitchOffsetDegrees) / 3;
        }
        if (new Vector2(Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees).magnitude > MaxCorrectionDegrees)
        { Reject($"Mean offset {new Vector2(Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees).magnitude:F1} degrees exceeds the {MaxCorrectionDegrees:F0}-degree limit. Refit and run HTC eye calibration, or skip without an offset.", now); return; }
        if (TargetIndex >= FitPointCount && (point.afterMedianDegrees > MaxMedianErrorDegrees ||
            point.afterP90Degrees > MaxP90ErrorDegrees || point.afterMedianDegrees > point.beforeMedianDegrees + .1f ||
            point.afterP90Degrees > point.beforeP90Degrees + .25f))
        { Reject("Separate dot check failed. Run HTC calibration and retry; no correction applied.", now); return; }
        if (TargetIndex < PointCount - 1) { TargetIndex++; StartPoint(now); return; }
        float improvement = 0;
        for (int i = FitPointCount; i < PointCount; i++)
            improvement += Report.points[i].beforeMedianDegrees - Report.points[i].afterMedianDegrees;
        improvement /= PointCount - FitPointCount;
        if (new Vector2(Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees).magnitude >= .25f && improvement < .25f)
        { Reject("No clear improvement on separate dots. No correction applied.", now); return; }
        if (GuidedEntry)
        {
            IsRunning = false; AwaitingConfirmation = true; m_State = "awaiting_confirmation";
            Report.outcome = "awaiting_confirmation";
            Status = Report.message = "Check all three shapes light up, then confirm OK.";
            return;
        }
        IsRunning = false; IsActive = true; m_State = "passed";
        Status = "Quick alignment passed (pilot). Applied for this participant and gaze source.";
        Report.outcome = "passed"; Report.finishedAt = now; Report.message = Status;
    }

    void Reject(string reason, double now)
    {
        IsRunning = IsActive = AwaitingConfirmation = false; m_State = "rejected"; Status = reason;
        Report.outcome = "rejected"; Report.finishedAt = now; Report.message = reason;
    }

    public void CancelOrClear(string reason, double now)
    {
        if (!IsRunning && !IsActive && !AwaitingConfirmation) return;
        if (IsRunning || AwaitingConfirmation) { Report.outcome = "cancelled"; Report.finishedAt = now; Report.message = reason; }
        IsRunning = IsActive = AwaitingConfirmation = false; m_State = "cleared"; Status = reason + " No quick correction applied.";
        m_Window.Clear();
    }

    // Called after selection of the usable EXT/native ray, before guidance/logging consume it.
    // Rotation belongs in head coordinates: a constant world yaw would fail when the wearer turns.
    public void Apply(GazeObservation sample)
    {
        if ((IsActive || AwaitingConfirmation) && sample.valid && !SameSource(sample))
            CancelOrClear("Participant or gaze source changed; repeat alignment.", sample.receiptTime);
        sample.alignmentId = Report?.id;
        sample.alignmentStatus = m_State;
        if (!IsActive || !sample.valid) return;
        if (!sample.headValid || !Finite(sample.headRotation))
        { sample.valid = false; sample.invalidReason = "alignment_requires_valid_head_pose"; return; }
        sample.worldDirection = sample.headRotation * CorrectHeadDirection(
            Quaternion.Inverse(sample.headRotation) * sample.worldDirection,
            Report.yawCorrectionDegrees, Report.pitchCorrectionDegrees);
        sample.alignmentApplied = true;
        sample.alignmentYawDegrees = Report.yawCorrectionDegrees;
        sample.alignmentPitchDegrees = Report.pitchCorrectionDegrees;
        sample.correction = Report.baseCorrection + "+" + Report.method;
    }

    bool SameSource(GazeObservation s) => s.deviceId == Report.deviceId && s.participant == Report.participant &&
        s.raySource == Report.raySource && s.correction == Report.baseCorrection;

    static Vector2 Angles(Vector3 direction) => new Vector2(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg,
        Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg);

    static Vector3 CorrectHeadDirection(Vector3 direction, float yaw, float pitch)
    {
        Vector2 angles = Angles(direction);
        float az = (angles.x + yaw) * Mathf.Deg2Rad;
        float el = Mathf.Clamp(angles.y + pitch, -89.9f, 89.9f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az));
    }

    static float Mean(List<float> values)
    {
        float sum = 0; foreach (float value in values) sum += value;
        return sum / values.Count;
    }

    static float Percentile(List<float> values, float fraction)
    {
        values.Sort(); float at = (values.Count - 1) * fraction; int low = (int)at;
        return Mathf.Lerp(values[low], values[Math.Min(low + 1, values.Count - 1)], at - low);
    }
    static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    static bool Finite(Quaternion q) => float.IsFinite(q.x) && float.IsFinite(q.y) && float.IsFinite(q.z) &&
        float.IsFinite(q.w) && Mathf.Abs(Quaternion.Dot(q, q) - 1) < .01f;

    struct Measurement { public double time; public long sequence; public Vector3 measured, expected; public float yaw, pitch; }

    [Serializable]
    public sealed class AlignmentReport
    {
        public const string Method = "head_local_yaw_pitch_quick_alignment_v1";
        public int schema = 2, deviceId, fitPointCount = 1;
        public bool assumedFixation, userConfirmed;
        public float attentionCueSeconds;
        public string method = Method, id, participant, raySource, baseCorrection, recordingId, utcStarted,
            outcome = "collecting", message;
        public double startedAt, finishedAt;
        public float yawCorrectionDegrees, pitchCorrectionDegrees;
        public string scope = "pilot_three_point_check_not_vendor_calibration_or_task_wide_accuracy";
        public float settleSeconds = SettleSeconds, collectionSeconds = CollectionSeconds, timeoutSeconds = MaxPointSeconds,
            maxCorrectionDegrees = MaxCorrectionDegrees, maxSpreadP90Degrees = MaxSpreadDegrees,
            maxMedianErrorDegrees = MaxMedianErrorDegrees, maxP90ErrorDegrees = MaxP90ErrorDegrees;
        public int minimumInputUpdates = MinimumUpdates;
        public List<AlignmentPoint> points = new List<AlignmentPoint>();
    }
    [Serializable]
    public sealed class AlignmentPoint
    {
        public int index, updates;
        public float meanYawOffsetDegrees, meanPitchOffsetDegrees;
        public bool assumedFixation;
        public string purpose;
        public Vector3 targetWorld;
        public long firstSequence, lastSequence;
        public double firstInputTime, lastInputTime;
        public float beforeMedianDegrees, beforeP90Degrees, afterMedianDegrees, afterP90Degrees, spreadP90Degrees;
    }
}
