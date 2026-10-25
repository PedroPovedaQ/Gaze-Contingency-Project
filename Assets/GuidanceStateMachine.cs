using System;

public enum GuidancePhase { Inactive, Opening, Turning, Refining, AcquiringTarget, Acquired, ReadyToSelect, Suspended, Paused, Completed, Stopped, PointingTarget, TimedOut }
public enum GuidanceAction { None, Direction, Progress, Arrival, ConfirmGaze, ConfirmPointing, SelectionReminder, ReturnTarget, ReturnBearing, ReturnWall }

/// <summary>Single owner of guidance qualification, phase transitions and action priority.
/// Step once per rendered observation; async callbacks only read state and recheck geometry.
/// Qualification never consumes an action: Commit is called only after accepted playback.</summary>
public sealed class GuidanceStateMachine
{
    public struct Input
    {
        public double now, deltaSeconds, receiptTime;
        public long observationSequence;
        public bool valid, inside, looking, controllerValid, controllerOnTarget, suppressVertical;
        public string direction;
        public float verticalFraction, remainingTurn;
        public GazeTargetHistory.Sample? history;
    }
    public readonly GuidanceTurnState Turn = new GuidanceTurnState();
    public readonly GazeTargetHistory TargetHistory = new GazeTargetHistory();
    readonly GazeZoneEntryGate m_Wall = new GazeZoneEntryGate(6, GuidanceSpeechPolicy.DirectionSettleSeconds);
    readonly GazeZoneEntryGate m_Pointing = new GazeZoneEntryGate(0, 1);
    readonly GazeZoneEntryGate m_Looking = new GazeZoneEntryGate(0, DirectionalHintPolicy.LookingHoldSeconds, DirectionalHintPolicy.LookingExitSeconds);
    readonly GazeReturnCueGate m_Revisit = new GazeReturnCueGate(), m_Return = new GazeReturnCueGate();
    readonly StudyPromptDelay m_Reminder = new StudyPromptDelay(3);
    public GuidancePhase Phase { get; private set; } = GuidancePhase.Inactive;
    public GuidancePhase PreviousPhase { get; private set; } = GuidancePhase.Inactive;
    public GuidanceAction Action { get; private set; }
    public long Revision { get; private set; }
    public double EnteredAt { get; private set; }
    public string TransitionReason { get; private set; } = "initial";
    public string Candidate { get; private set; }
    public double CandidateSince { get; private set; }
    int m_VerticalBand;
    public bool Stable { get; private set; }
    public bool TargetAcknowledged { get; private set; }
    public bool LookingStable => m_Looking.InsideStable;
    public bool PointingStable => m_Pointing.InsideStable;
    public bool WallStable => m_Wall.InsideStable;
    public bool DebouncingWallExit => m_Return.IsDebouncingExit;
    public double LastTargetVisitTime { get; private set; } = -1;
    public long LastTargetVisitSequence { get; private set; } = -1;
    public string PendingBearingDirection(double now, bool suppressVertical)
    {
        string direction = TargetHistory.PendingDirection(now);
        return suppressVertical && DirectionalHintPolicy.HasVertical(direction) ? null : direction;
    }
    public bool RevisitPending(double now) => m_Revisit.IsPending(now);
    public bool WallReturnPending(double now) => m_Return.IsPending(now);
    public bool Allows(GuidanceAction action) => Action == action && action != GuidanceAction.None;
    public bool Allows(long revision, GuidanceAction action) => Revision == revision && Allows(action);

    // A controller hit blocks search immediately; this is not a qualified confirmation.
    // Re-read live controller evidence here before AudioSource.Play, because a response
    // can arrive after controller telemetry but before the next state-machine Step.
    public bool AllowsPlayback(long revision, GuidanceAction action, bool controllerValid, bool controllerOnTarget) =>
        Allows(revision, action) && !(controllerValid && controllerOnTarget && IsSearchAction(action));
    public static bool IsSearchAction(GuidanceAction action) => action == GuidanceAction.Direction ||
        action == GuidanceAction.Progress || action == GuidanceAction.Arrival || action == GuidanceAction.ReturnTarget ||
        action == GuidanceAction.ReturnBearing || action == GuidanceAction.ReturnWall;

    void Transition(GuidancePhase phase, string reason, double now, bool force = false)
    {
        if (!force && Phase == phase) return;
        PreviousPhase = Phase; Phase = phase; TransitionReason = reason; EnteredAt = now; Revision++;
    }
    void ResetSpatial()
    {
        Turn.Reset(); TargetHistory.Reset(); m_Wall.Reset(); m_Pointing.Reset(); m_Looking.Reset();
        m_Revisit.Reset(); m_Return.Reset(); Candidate = null; Stable = false; Action = GuidanceAction.None;
        LastTargetVisitTime = -1; LastTargetVisitSequence = -1;
    }
    public void BeginSearch(double now, string openingDirection = null)
    {
        ResetSpatial(); m_Reminder.Reset(); TargetAcknowledged = false; Turn.Prime(openingDirection);
        Transition(GuidancePhase.Suspended, "search_started_awaiting_evidence", now, true);
    }
    public void ResumeSearch(double now)
    {
        ResetSpatial(); m_Reminder.Step(false, 0);
        Transition(GuidancePhase.Suspended, "search_resumed_awaiting_evidence", now, true);
    }
    public void SetLifecycle(GuidancePhase phase, string reason, double now)
    {
        ResetSpatial(); m_Reminder.Step(false, 0); Transition(phase, reason, now);
    }
    public void Step(Input input)
    {
        Turn.Observe(input.valid && !input.inside, input.direction, input.remainingTurn, input.now);
        bool looking = input.valid && input.looking;
        bool revisit = m_Revisit.Update(input.valid, looking, input.now);
        bool reminder = m_Reminder.Step(looking && TargetAcknowledged, input.deltaSeconds);
        bool lookEntry = m_Looking.Update(input.valid, looking, input.now);
        // Controller hold continues through eye blinks, but cannot authorize speech alone.
        bool pointEntry = m_Pointing.Update(input.controllerValid, input.controllerOnTarget, input.now);
        bool wallEntry = m_Wall.Update(input.valid, input.inside, input.now);
        bool returning = m_Return.Update(input.valid, input.inside, input.now);
        if (input.valid && input.history.HasValue) TargetHistory.Observe(true, input.history.Value);
        else TargetHistory.Clear();
        Action = GuidanceAction.None;
        if (!input.valid)
        {
            Candidate = null; Stable = false;
            Transition(GuidancePhase.Suspended, "gaze_or_geometry_unavailable", input.now); return;
        }
        int band = DirectionalHintPolicy.VerticalBand(input.verticalFraction);
        if (Candidate != input.direction || DirectionalHintPolicy.HasVertical(input.direction) && band != m_VerticalBand)
        { Candidate = input.direction; m_VerticalBand = band; CandidateSince = input.now; }
        Stable = input.now - CandidateSince >= GuidanceSpeechPolicy.DirectionSettleSeconds;
        if (looking && LookingStable)
        { LastTargetVisitTime = input.receiptTime; LastTargetVisitSequence = input.observationSequence; }
        var phase = ResolvePhase(input, looking);
        Transition(phase, PhaseReason(phase), input.now);
        if (looking)
        {
            if (input.controllerValid && input.controllerOnTarget && pointEntry) Action = GuidanceAction.ConfirmPointing;
            else if (lookEntry) Action = GuidanceAction.ConfirmGaze;
            else if (reminder) Action = GuidanceAction.SelectionReminder;
            return; // Never nudge away from the target while qualifying/holding gaze.
        }
        // Suppress search even when the eyes have not reached the winning target
        // vicinity. Keep the hold timer and gaze validity requirements unchanged.
        if (input.controllerValid && input.controllerOnTarget) return;
        if (!Stable || input.direction == null) return;
        if (revisit && input.direction != DirectionalHintPolicy.AlignedPhrase) Action = GuidanceAction.ReturnTarget;
        else if (PendingBearingDirection(input.now, input.suppressVertical) != null) Action = GuidanceAction.ReturnBearing;
        else if (wallEntry) Action = GuidanceAction.Arrival;
        else if (returning) Action = GuidanceAction.ReturnWall;
        else if (!m_Return.IsDebouncingExit && (!input.inside || WallStable))
            Action = !input.inside && Turn.ProgressDue(input.direction, input.remainingTurn) ? GuidanceAction.Progress : GuidanceAction.Direction;
    }
    GuidancePhase ResolvePhase(Input input, bool looking)
    {
        if (input.controllerValid && input.controllerOnTarget && (!looking || !PointingStable)) return GuidancePhase.PointingTarget;
        if (!looking) return input.inside ? GuidancePhase.Refining : GuidancePhase.Turning;
        if (!LookingStable) return GuidancePhase.AcquiringTarget;
        return input.controllerValid && input.controllerOnTarget ? GuidancePhase.ReadyToSelect : GuidancePhase.Acquired;
    }
    static string PhaseReason(GuidancePhase phase)
    {
        switch (phase)
        {
            case GuidancePhase.PointingTarget: return "controller_target_waiting_for_hold_and_gaze";
            case GuidancePhase.Refining: return "target_wall_entered";
            case GuidancePhase.Turning: return "coarse_direction_needed";
            case GuidancePhase.AcquiringTarget: return "target_vicinity_entered";
            case GuidancePhase.ReadyToSelect: return "qualified_gaze_and_controller_target";
            default: return "target_gaze_qualified";
        }
    }
    public void SuppressBearing(double now)
    { TargetHistory.MarkSpoken(now); m_Return.MarkSpoken(now); Action = GuidanceAction.None; }
    public void Commit(GuidanceAction action, double now, bool inside, bool controllerOnTarget = false, string direction = null, float remainingTurn = 0)
    {
        switch (action)
        {
            case GuidanceAction.Direction:
            case GuidanceAction.Progress: if (!inside) Turn.MarkSpoken(direction, remainingTurn, now, action == GuidanceAction.Direction); break;
            case GuidanceAction.ConfirmPointing:
                m_Pointing.MarkSpoken(now); m_Looking.MarkSpoken(now); TargetAcknowledged = true;
                if (inside) m_Wall.MarkSpoken(now); TargetHistory.Clear(); m_Return.MarkSpoken(now); break;
            case GuidanceAction.ConfirmGaze:
                m_Looking.MarkSpoken(now); TargetAcknowledged = true;
                // Looking confirmation does not consume the independent pointing hold.
                if (inside) m_Wall.MarkSpoken(now); TargetHistory.Clear(); m_Return.MarkSpoken(now); break;
            case GuidanceAction.SelectionReminder: m_Reminder.MarkSpoken(); break;
            case GuidanceAction.ReturnTarget: m_Revisit.MarkSpoken(now); TargetHistory.Clear(); m_Return.MarkSpoken(now); break;
            case GuidanceAction.ReturnBearing: TargetHistory.MarkSpoken(now); m_Return.MarkSpoken(now); if (inside) m_Wall.MarkSpoken(now); break;
            case GuidanceAction.Arrival: m_Wall.MarkSpoken(now); break;
            case GuidanceAction.ReturnWall: m_Return.MarkSpoken(now); break;
        }
        Action = GuidanceAction.None;
    }
    public GuidanceStateSnapshot Snapshot(double now, long sequence, bool gazeValid, bool controllerValid,
        string gazeHit = null, string winner = null, string controllerHit = null, string target = null) => new GuidanceStateSnapshot {
        phase = Phase.ToString(), previousPhase = PreviousPhase.ToString(), transitionReason = TransitionReason,
        allowedAction = Action.ToString(), revision = Revision, enteredAtSeconds = EnteredAt, capturedAtSeconds = now,
        observationSequence = sequence, gazeValid = gazeValid, controllerValid = controllerValid,
        newGuidanceAllowed = gazeValid && Action != GuidanceAction.None,
        gazeHitObjectId = gazeValid ? gazeHit : null, vicinityWinnerObjectId = gazeValid ? winner : null,
        controllerHitObjectId = controllerValid ? controllerHit : null, targetObjectId = target
    };
}
