using System;
class GazeQualityChecks
{
    static void Check(bool ok,string name) { if(!ok) throw new Exception(name); }
    static int Main()
    {
        GazeTrackingFeedbackChecks.Run();
        // Reproduce go-back -> YEP -> look-down: no automatic cue may splice a
        // sentence; the following pause is measured from actual completion.
        Check(GuidanceSpeechPolicy.CanConfirmTargetDuringSpeech("gaze_target_vicinity_hold",.35,2,10,0), "Qualified target opens faster bounded interruption");
        Check(!GuidanceSpeechPolicy.CanConfirmTargetDuringSpeech("stable_direction",1,2,10,0), "Ordinary direction has no fast override");
        Check(!GuidanceSpeechPolicy.CanConfirmTargetDuringSpeech("gaze_target_vicinity_hold",.34,2,10,0), "Protect onset");
        Check(!GuidanceSpeechPolicy.CanConfirmTargetDuringSpeech("controller_target_gaze_vicinity",1,.49,10,0), "Protect ending");
        Check(!GuidanceSpeechPolicy.CanConfirmTargetDuringSpeech("gaze_target_vicinity_hold",1,2,10,9), "Protect interruption cooldown");
        Check(!GuidanceSpeechPolicy.CanStart(true,10,-100,false),"A qualified 0.1-second gaze cannot cut an ongoing go-back phrase");
        Check(!GuidanceSpeechPolicy.CanStart(false,10.24,10,false),"A return cue needs a post-playback pause before YEP");
        Check(GuidanceSpeechPolicy.CanStart(false,10.26,10,false),"Fresh confirmation becomes eligible after the pause");
        Check(!GuidanceSpeechPolicy.CanStart(false,12.09,12,true),"After YEP, directions retain a 100ms acknowledgment pause");
        Check(GuidanceSpeechPolicy.CanStart(false,12.11,12,true),"Latest direction is eligible after acknowledgment settles");
        for(int i=0;i<4;i++) Check(GuidanceSpeechPolicy.IsAcknowledgment(DirectionalHintPolicy.Variant(DirectionalHintPolicy.LookingPhrase,i)),"All acknowledgment variants receive the same breathing room");
        Check(GuidanceSpeechPolicy.IsAcknowledgment(DirectionalHintPolicy.PointingPhrase),"Controller acknowledgment is protected by the same pause");
        Check(!GuidanceSpeechPolicy.IsAcknowledgment(DirectionalHintPolicy.ReturnToTarget(DirectionalHintPolicy.DownLeftPhrase)),"Return cue uses ordinary pacing");
        var turn = new GuidanceTurnState();
        Check(DirectionalHintPolicy.SignedYaw(0,1,0,-1) == 180, "0 to 180 tie turns right");
        Check(DirectionalHintPolicy.SignedYaw(0,-1,0,1) == -180, "180 to 0 tie turns left");
        Check(DirectionalHintPolicy.Direction(0,1,-.02f,-1) == DirectionalHintPolicy.LeftPhrase, "Real lateral offset chooses shorter left turn near 180");
        Check(turn.Resolve(179,false) == DirectionalHintPolicy.RightPhrase, "Choose initial right turn");
        Check(turn.Resolve(-179,false) == DirectionalHintPolicy.RightPhrase && turn.Remaining(-179) == 181, "Rear-seam jitter retains committed turn and unwrapped distance");
        turn.MarkSpoken(DirectionalHintPolicy.RightPhrase,180);
        Check(!turn.ProgressDue(DirectionalHintPolicy.RightPhrase,150), "Small progress does not earn another cue");
        foreach (float milestone in new[]{135f,90f,45f})
        {
            Check(turn.ProgressDue(DirectionalHintPolicy.RightPhrase,milestone), "New 45 degree milestone earns encouragement");
            turn.MarkSpoken(DirectionalHintPolicy.RightPhrase,milestone);
            Check(!turn.ProgressDue(DirectionalHintPolicy.RightPhrase,milestone), "Stationary gaze cannot replay a milestone");
            Check(!turn.ProgressDue(DirectionalHintPolicy.RightPhrase,milestone+20), "Backtracking cannot earn encouragement");
            turn.MarkSpoken(DirectionalHintPolicy.RightPhrase,milestone+20);
            Check(!turn.ProgressDue(DirectionalHintPolicy.RightPhrase,milestone), "Explicit reminder does not reset consumed progress");
        }
        turn.Resolve(15,true);
        Check(turn.Resolve(-45,false) == DirectionalHintPolicy.LeftPhrase, "Overshoot uses actual return direction after arrival");
        turn.Prime(DirectionalHintPolicy.LeftPhrase);
        Check(turn.Resolve(179,false) == DirectionalHintPolicy.LeftPhrase, "Opening direction survives rear seam when search starts");
        Check(DirectionalHintPolicy.ReturnToTarget(DirectionalHintPolicy.DownLeftPhrase) == "Go back down and left.", "Recent target departure supports diagonal return");
        var revisit = new GazeReturnCueGate();
        revisit.Update(true,true,0); revisit.Update(true,true,.11);
        Check(!revisit.Update(true,false,.2) && revisit.Update(true,false,.46), "Recent target visit needs a sustained real departure");
        revisit.Update(false,false,.5);
        Check(!revisit.Update(true,false,.9), "Blink does not invent a recent target return");
        foreach(var direction in new[]{DirectionalHintPolicy.LeftPhrase,DirectionalHintPolicy.RightPhrase,DirectionalHintPolicy.UpPhrase,DirectionalHintPolicy.DownPhrase,DirectionalHintPolicy.UpLeftPhrase,DirectionalHintPolicy.UpRightPhrase,DirectionalHintPolicy.DownLeftPhrase,DirectionalHintPolicy.DownRightPhrase})
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),DirectionalHintPolicy.ReturnToTarget(direction))>=0,"Every target-return diagonal is prepared");
        foreach (var direction in DirectionalHintPolicy.ProgressDirections)
            for(int i=0;i<3;i++)
            {
                string cue=DirectionalHintPolicy.Progress(direction,i);
                Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),cue)>=0 && DirectionalHintPolicy.IsContinuation(cue),"Directional progress variants are prepared and retain continuation limits");
            }
        Check(DirectionalHintPolicy.Progress(DirectionalHintPolicy.LeftPhrase,0)=="Keep going to the left.","Left route is explicit");
        Check(DirectionalHintPolicy.Progress(DirectionalHintPolicy.RightPhrase,0)=="Keep going to the right.","Right route is explicit");
        Check(DirectionalHintPolicy.Progress(DirectionalHintPolicy.DownLeftPhrase,0)=="Keep looking down and left.","Diagonal progress retains both axes");
        foreach (var direction in new[] { DirectionalHintPolicy.LeftPhrase, DirectionalHintPolicy.RightPhrase })
        {
            var moving = new GuidanceTurnState();
            moving.MarkSpoken(direction, 180, 0);
            moving.Observe(true, direction, 180, 0);
            moving.Observe(true, direction, 177, .1);
            Check(moving.BriefProgressAllowed(direction, .1), "Recent same-route movement permits short encouragement");
            Check(!moving.BriefProgressAllowed(direction == DirectionalHintPolicy.LeftPhrase ?
                DirectionalHintPolicy.RightPhrase : DirectionalHintPolicy.LeftPhrase, .1), "Short encouragement cannot describe the opposite route");
            moving.Observe(true, direction, 179, .2);
            Check(!moving.BriefProgressAllowed(direction, .2), "Reversing immediately removes short encouragement");
            moving.Observe(true, direction, 175, .3);
            Check(moving.BriefProgressAllowed(direction, .3), "Resuming measured progress permits encouragement");
            moving.Observe(true, direction, 175, .5);
            moving.Observe(true, direction, 175, .7);
            Check(!moving.BriefProgressAllowed(direction, .7), "Stopping expires progress even with valid gaze");
            moving.Observe(false, direction, 175, .8);
            moving.Observe(true, direction, 174, .9);
            moving.Observe(true, direction, 170, 1);
            Check(!moving.BriefProgressAllowed(direction, 1), "Tracking recovery needs an explicit direction again");
            moving.MarkSpoken(direction, 170, 1);
            moving.Observe(true, direction, 130, 7);
            moving.Observe(true, direction, 127, 7.1);
            Check(!moving.BriefProgressAllowed(direction, 7.1), "A direction older than six seconds cannot support a vague cue");
        }
        for (int i = 0; i < 3; i++)
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.BriefProgress(i)) >= 0 &&
                DirectionalHintPolicy.IsContinuation(DirectionalHintPolicy.BriefProgress(i)),
                "Short turn-progress clips are prepared and retain continuation limits");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),"Keep going.")<0 &&
            Array.IndexOf(DirectionalHintPolicy.AllPhrases(),"Keep going that way.")<0,"Directionless progress clips are no longer prepared");
        var reminder = new StudyPromptDelay(5);
        Check(DirectionalHintPolicy.VerticalBand(.2f) == 0 && DirectionalHintPolicy.VerticalBand(-.2f) == 0, "Exactly 20 percent is little, in either direction");
        Check(DirectionalHintPolicy.VerticalBand(.2001f) == 1 && DirectionalHintPolicy.VerticalBand(.5f) == 1, "Over 20 through 50 percent is ordinary");
        Check(DirectionalHintPolicy.VerticalBand(.5001f) == 2 && DirectionalHintPolicy.VerticalBand(-1.2f) == 2, "Over 50 percent is way, including off-wall gaze");
        Check(DirectionalHintPolicy.Variant(DirectionalHintPolicy.UpPhrase, 0, verticalFraction: .2f) == "A little higher.", "Little up means small spatial correction");
        Check(DirectionalHintPolicy.Variant(DirectionalHintPolicy.DownPhrase, 0, verticalFraction: .3f) == "Look lower.", "Medium down is not called little");
        Check(DirectionalHintPolicy.Variant(DirectionalHintPolicy.UpPhrase, 0, verticalFraction: .6f) == "Look way higher.", "Large up uses way");
        Check(DirectionalHintPolicy.AreaEntry(DirectionalHintPolicy.DownRightPhrase, 0, .6f).Contains("Look way lower and right"), "Arrival diagonal respects vertical distance");
        foreach (float fraction in new[] { .1f, .3f, .7f })
        foreach (string direction in new[] { DirectionalHintPolicy.UpPhrase, DirectionalHintPolicy.DownPhrase, DirectionalHintPolicy.UpLeftPhrase, DirectionalHintPolicy.DownRightPhrase })
        for (int i = 0; i < 4; i++)
        {
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.Variant(direction, i, verticalFraction: fraction)) >= 0, "Every distance/diagonal phrase is prepared");
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.AreaEntry(direction, i, fraction)) >= 0, "Every distance/arrival phrase is prepared");
        }
        for (int i = 0; i < 49; i++) Check(!reminder.Step(true, .1), "No cross reminder before five seconds");
        Check(reminder.Step(true, .11), "Cross reminder becomes due after five seconds");
        reminder.MarkSpoken();
        Check(!reminder.Step(true, 10), "A reminder is not repeated in the same round");
        reminder.Reset(); reminder.Step(true, .1); reminder.Step(false, .1);
        for (int i = 0; i < 49; i++) Check(!reminder.Step(true, .1), "Pause or looking at the cross resets the wait");
        reminder.Reset();
        Check(!reminder.Step(true, 6), "Unobserved frame gap cannot supply a sustained look");
        var selectionReminder = new StudyPromptDelay(3);
        for (int i = 0; i < 29; i++) Check(!selectionReminder.Step(true, .1), "Selection reminder waits for continued gaze");
        selectionReminder.Step(false, .1);
        Check(!selectionReminder.Step(true, .1), "Lost or non-target gaze clears the selection hold");
        var crossPrompts = new System.Collections.Generic.HashSet<string>();
        for (int round = 0; round < 40; round++)
        {
            string prompt = DirectionalHintPolicy.ReadinessPrompt(round);
            crossPrompts.Add(prompt);
            Check(prompt.Contains("cross") && prompt.Contains("next round"), "Transition directs attention to readiness cross");
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), prompt) >= 0, "Transition cue is prepared for both voices");
            Check(prompt == DirectionalHintPolicy.ReadinessPrompt(round + 3), "Transition variation is deterministic");
        }
        Check(crossPrompts.Count == 3, "Three distinct cross prompts rotate between rounds");
        Check(DirectionalHintPolicy.ReadinessPrompt(-1) == DirectionalHintPolicy.ReadinessPrompt(2), "Practice index safely selects a cue");
        Check(DirectionalHintPolicy.ReadinessReminder(-30, 20).Contains("above and to your left"), "Cross can be diagonally above gaze");
        Check(DirectionalHintPolicy.ReadinessReminder(30, -20).Contains("below and to your right"), "Cross can be diagonally below gaze");
        Check(DirectionalHintPolicy.ReadinessReminder(0, 0).Contains("ahead of you"), "Aligned cross uses ahead wording");
        foreach (float yaw in new[] {-179f, -30f, 0f, 30f, 179f})
        foreach (float pitch in new[] {-20f, 0f, 20f})
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.ReadinessReminder(yaw, pitch)) >= 0,
                "Every cross reminder must be prepared for both voices");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.SelectionReminderPhrase) >= 0,
            "Selection reminder must be prepared before search");
        Check(DirectionalHintPolicy.SelectionReminder(true) == DirectionalHintPolicy.TriggerReminderPhrase,
            "An aimed controller gets trigger-only wording");
        Check(DirectionalHintPolicy.SelectionReminder(false) == DirectionalHintPolicy.SelectionReminderPhrase,
            "A controller off target still gets aiming instructions");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(), DirectionalHintPolicy.TriggerReminderPhrase) >= 0 &&
            DirectionalHintPolicy.IsSelectionReminder(DirectionalHintPolicy.TriggerReminderPhrase),
            "Trigger-only reminder is prepared and protected from interruption");
        Check(GazeQualityPolicy.IsFresh(true, 10, 10.05, .1), "fresh tracked sample");
        Check(!GazeQualityPolicy.IsFresh(false, 10, 10.05, .1), "invalid sample");
        Check(!GazeQualityPolicy.IsFresh(true, 10, 10.2, .1), "stale sample");
        Check(!GazeQualityPolicy.IsFresh(true, double.NaN, 10, .1), "unknown sample time");
        Check(!GazeQualityPolicy.IsFresh(true, 11, 10, .1), "future timestamp");
        Check(GazeQualityPolicy.Vertical(0, 15, 3) == "Look up.", "target above gaze");
        Check(GazeQualityPolicy.Vertical(15, 0, 3) == "Look down.", "target below gaze");
        Check(GazeQualityPolicy.Vertical(15, 16, 3) == "Right there.", "vertical alignment");
        var gate = new GazeZoneEntryGate();
        gate.Update(true,true,0); Check(gate.Update(true,true,.11),"stable entry");gate.MarkSpoken(.11);
        Check(!gate.Update(true,true,8),"vertical scanning stays in same sector");
        gate.Update(false,false,9);Check(!gate.Update(true,true,10),"blink cannot rearm entry");
        var pointing = new GazeZoneEntryGate(0, 1.0);
        pointing.Update(true,true,0);
        Check(!pointing.Update(true,true,.99),"brief controller hit cannot announce target");
        Check(pointing.Update(true,true,1.0),"continuous 1 second target hold qualifies");pointing.MarkSpoken(1.0);
        Check(!pointing.Update(true,true,3),"holding the target does not repeat confirmation");
        pointing.Update(false,false,3.1);pointing.Update(true,true,3.2);
        Check(!pointing.Update(true,true,4.8),"blink does not rearm spoken confirmation");
        pointing.Update(true,false,5);pointing.Update(true,false,5.51);pointing.Update(true,true,6);
        Check(!pointing.Update(true,true,6.99),"reacquisition needs a new full hold");
        Check(pointing.Update(true,true,7.0),"genuine reacquisition can confirm after full hold");
        pointing.Reset();pointing.Update(true,true,0);pointing.Update(false,false,.9);pointing.Update(true,true,1.0);
        Check(!pointing.Update(true,true,1.99),"tracking gap resets incomplete hold");
        Check(pointing.Update(true,true,2.0),"fresh full hold after tracking recovery");
        Check(DirectionalHintPolicy.PointingPhrase=="You're on it!","requested short confirmation");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",7,12,0,false)=="A bit more.","near-target improvement earns a short context-dependent continuation");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",7,12,1,false)=="Just a little more.","near-target continuation has variation");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",7,7,0,false)=="Look right.","standing still never gets a bit more");
        Check(DirectionalHintPolicy.FollowUp("Look left.","Look right.",7,12,0,false)=="Look left.","reversal needs a named direction");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",5,9,0,false,true)=="Look right.","continuation cannot loop");
        Check(DirectionalHintPolicy.FollowUp("Look up.","Look up.",7,12,0,false,false,.6f)=="Keep looking up.","large vertical remainder is not a bit more");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",40,60,0,true)=="Keep going to the right.","progressing same direction gets continuation");
        // Reproduce trial 1: every improving observation used to request Keep going again.
        float[] recordedYaw = {163.2f,124.7f,177.9f,162.7f,157f,103.2f,65.4f,31.2f};
        bool continuationUsed=false; int continuationCount=0;
        for(int i=1;i<recordedYaw.Length;i++)
        {
            var phrase=DirectionalHintPolicy.FollowUp("Look right.","Look right.",recordedYaw[i],recordedYaw[i-1],i,true,continuationUsed);
            if(DirectionalHintPolicy.IsContinuation(phrase)) { continuationCount++; continuationUsed=true; }
        }
        Check(continuationCount==1,"recorded first-trial progress must not loop Keep going");
        Check(DirectionalHintPolicy.FollowUp("Look right.","Look right.",65,60,0,true)=="Turn to your right.","moving away retains explicit direction");
        Check(DirectionalHintPolicy.FollowUp("Look left.","Look right.",40,60,0,true)=="Turn to your left.","direction change is explicit");
        Check(DirectionalHintPolicy.WithinArea(0,2.8f,"Look up.")=="Look up.","near-threshold jitter retains direction");
        Check(DirectionalHintPolicy.WithinArea(0,1,"Look up.")=="Right there.","alignment releases upward guidance");
        Check(DirectionalHintPolicy.WithinArea(0,3.5f,"Right there.")=="Right there.","small departure does not restart upward speech");
        Check(DirectionalHintPolicy.WithinArea(0,6,"Right there.")=="Look up.","meaningful departure restores direction");
        Check(DirectionalHintPolicy.WithinArea(-12,10)=="Look up and a little to your left.","same-wall diagonal up-left correction");
        Check(DirectionalHintPolicy.WithinArea(12,-10)=="Look down and a little to your right.","same-wall diagonal down-right correction");
        // Run26 actual playback geometry: the nearby capsule was lower than the
        // target, but the measured eye ray was above both. Never infer up/down
        // from a vicinity winner's position instead of the current gaze ray.
        Check(DirectionalHintPolicy.WithinArea(-20.807457f,-4.290212f)==DirectionalHintPolicy.DownLeftPhrase,
            "Run26 playback uses target-minus-gaze elevation, not target-minus-distractor height");
        Check(DirectionalHintPolicy.WithinArea(-20.8f,-2.749f,DirectionalHintPolicy.DownLeftPhrase)==DirectionalHintPolicy.DownLeftPhrase,
            "Run26 near-level continuation is existing hysteresis, not an inverted vertical sign");
        Check(DirectionalHintPolicy.WithinArea(-20.8f,-1.4f,DirectionalHintPolicy.DownLeftPhrase)==DirectionalHintPolicy.LeftPhrase,
            "Approaching level releases downward speech while preserving lateral guidance");
        bool suppressed;
        var rawRun26 = DirectionalHintPolicy.WithinArea(-20.807457f,-4.290212f);
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(rawRun26,true,.055331f,out suppressed)==DirectionalHintPolicy.LeftPhrase && suppressed,
            "Run26 visible capsule is below target: keep left and suppress conflicting downward component without changing raw gaze");
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(DirectionalHintPolicy.UpRightPhrase,true,-.055331f,out suppressed)==DirectionalHintPolicy.RightPhrase && suppressed,
            "Mirror case retains right but suppresses conflicting upward component");
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(DirectionalHintPolicy.DownPhrase,true,.055331f,out suppressed)==null && suppressed,
            "Vertical-only reference conflict stays quiet, never right-there or target confirmation");
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(rawRun26,false,.055331f,out suppressed)==rawRun26 && !suppressed,
            "Absent, ambiguous, target-self or different-wall reference preserves measured gaze direction");
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(rawRun26,true,-.055331f,out suppressed)==rawRun26 && !suppressed,
            "Consistent visible-reference height preserves the raw diagonal");
        float apparentHeightDelta=(float)((Math.Atan2(.5,6)-Math.Atan2(.4,2))*180/Math.PI);
        Check(apparentHeightDelta<0 && DirectionalHintPolicy.ConsistentWithVisibleReference(DirectionalHintPolicy.DownPhrase,
            true,apparentHeightDelta,out suppressed)==DirectionalHintPolicy.DownPhrase && !suppressed,
            "World-higher but visually-lower target at different distance keeps valid downward guidance");
        Check(DirectionalHintPolicy.ConsistentWithVisibleReference(rawRun26,false,0,out suppressed)!=
            DirectionalHintPolicy.ConsistentWithVisibleReference(rawRun26,true,.055331f,out suppressed),
            "A queued diagonal fails direction equality when a contradictory visible reference appears before playback");
        Check(DirectionalHintPolicy.WithinArea(-20.8f,4f,DirectionalHintPolicy.DownLeftPhrase)==DirectionalHintPolicy.UpLeftPhrase,
            "Passing below target reverses the vertical component despite prior downward state");
        Check(DirectionalHintPolicy.WithinArea(-17.3f,-18.53f)==DirectionalHintPolicy.DownLeftPhrase &&
            DirectionalHintPolicy.WithinArea(-20.8f,0,DirectionalHintPolicy.DownLeftPhrase)!=DirectionalHintPolicy.DownLeftPhrase,
            "Delayed down-left proposal loses directional eligibility if gaze becomes level before playback");
        Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),DirectionalHintPolicy.LookingPhrase)>=0,"gaze-only confirmation prepared for both voices");
        var looking = new GazeZoneEntryGate(0, DirectionalHintPolicy.LookingHoldSeconds, DirectionalHintPolicy.LookingExitSeconds);
        looking.Update(true,true,0);
        Check(!looking.Update(true,true,.099) && looking.Update(true,true,.1),"vicinity gaze needs the full 0.1 second hold");
        looking.MarkSpoken(.1); looking.Update(false,false,1); looking.Update(true,true,2);
        Check(!looking.Update(true,true,9),"blink and continued direct gaze do not repeat confirmation");
        looking.Reset(); looking.Update(true,true,0); looking.Update(true,true,.1); looking.MarkSpoken(.1);
        looking.Update(true,false,1); looking.Update(true,false,1.16); looking.Update(true,true,1.2);
        Check(looking.Update(true,true,1.301),"look away and return must confirm again without a six-second wait");
        looking.MarkSpoken(1.301); looking.Update(true,false,2); looking.Update(true,false,2.1); looking.Update(true,true,2.12);
        Check(!looking.Update(true,true,3),"brief boundary jitter must not rearm gaze confirmation");
        looking.Reset(); looking.Update(true,true,0); looking.Update(false,false,.04); looking.Update(true,true,1);
        Check(!looking.Update(true,true,1.099) && looking.Update(true,true,1.101),"invalid gaze resets incomplete vicinity hold");
        looking.Reset(); looking.Update(true,true,0); looking.Update(true,false,.04); looking.Update(true,true,1);
        Check(!looking.Update(true,true,1.099) && looking.Update(true,true,1.101),"leaving ring resets incomplete vicinity hold");
        for(int i=0;i<4;i++) {
            string phrase=DirectionalHintPolicy.Variant(DirectionalHintPolicy.LookingPhrase,i);
            Check(phrase!=DirectionalHintPolicy.Variant(DirectionalHintPolicy.LookingPhrase,i+1),"successive target confirmations vary");
            Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),phrase)>=0,"every target confirmation is prepared for both voices");
        }
        Check(DirectionalHintPolicy.WithinArea(-4,2,DirectionalHintPolicy.UpLeftPhrase)==DirectionalHintPolicy.UpLeftPhrase,
            "both diagonal axes retain correction inside their hysteresis bands");
        Check(DirectionalHintPolicy.WithinArea(-2,8,DirectionalHintPolicy.UpLeftPhrase)==DirectionalHintPolicy.UpPhrase,
            "resolved horizontal error drops only the horizontal part of a diagonal");
        foreach(var d in new[]{DirectionalHintPolicy.UpLeftPhrase,DirectionalHintPolicy.UpRightPhrase,DirectionalHintPolicy.DownLeftPhrase,DirectionalHintPolicy.DownRightPhrase})
            for(int i=0;i<3;i++) Check(Array.IndexOf(DirectionalHintPolicy.AllPhrases(),DirectionalHintPolicy.Variant(d,i))>=0,
                "every diagonal variation is present in prepared audio");
        var back=new GazeReturnCueGate();back.Update(true,true,0);back.Update(true,true,.2);
        back.Update(false,false,.3);Check(!back.Update(true,false,.7),"loss is not overshoot");
        GazeTargetHistoryChecks.Run();
        Console.WriteLine("PASS gaze freshness, vertical direction, same-wall scan and tracking gap gates");return 0;
    }
}
