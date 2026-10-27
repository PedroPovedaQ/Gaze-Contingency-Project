using System;
using UnityEngine;

public static class TrialReviewGuidanceStateChecks
{
    static void Assert(bool value,string message){if(!value)throw new Exception("Guidance state: "+message);}
    public static void Run(TrialReplayTimeline timeline)
    {
        var diagnostic = new AssistanceRuntimeSnapshot { mode="adaptive", provider="local_minilm",
            modelVersion=AssistanceRuntimeSnapshot.LocalModelVersion, connection="ready", capturedAtSeconds=12,
            lastDecisionId="0123456789abcdef0123456789abcdef", lastDecisionProvider="local_minilm",
            lastStrategy="observe_4s", lastOutcome="observing", lastDecisionAtSeconds=10 };
        var restored = JsonUtility.FromJson<GuidanceStateSnapshot>(JsonUtility.ToJson(new GuidanceStateSnapshot { phase="Refining", assistance=diagnostic }));
        Assert(restored.assistance.provider=="local_minilm" && restored.assistance.lastStrategy=="observe_4s" &&
            restored.assistance.LastDecisionLabel.Contains("2.0s ago"), "provider and accepted observation survive recording with cursor-relative age");
        Assert(new AssistanceRuntimeSnapshot().SelectionLabel=="Not recorded" && new AssistanceRuntimeSnapshot().LastDecisionLabel=="No accepted model decision",
            "legacy/default diagnostic object never invents a model or decision");
        timeline.Seek(1);
        var sample=timeline.State.Sample;
        var original=sample.guidanceState;
        try
        {
            var index=new TrialReviewGuidanceState();
            index.Prepare(timeline,1,long.MaxValue,false);
            Assert(index.Current==null&&index.Availability.Contains("not recorded"),"legacy is unavailable, not inferred");
            sample.guidanceState=new GuidanceStateSnapshot { phase="Refining",previousPhase="Turning",revision=2,
                gazeValid=true,controllerValid=true,gazeHitObjectId="different-object",vicinityWinnerObjectId=timeline.State.Trial.targetId };
            index=new TrialReviewGuidanceState();
            index.Prepare(timeline,1,long.MaxValue,false);
            Assert(index.Current==sample.guidanceState,"recorded snapshot at cursor");
            index.Prepare(timeline,1,sample.sequence-1,false);
            Assert(index.Current==null,"exact sequence excludes future snapshot");
            index.Prepare(timeline,1,long.MaxValue,true);
            Assert(index.Current==null&&index.Availability=="Waiting for headset","disconnect hides stale state");
            timeline.Seek(8.2);index.Prepare(timeline,8.2,long.MaxValue,false);
            Assert(index.Current==null,"sample gap hides state");
            timeline.Seek(21);index.Prepare(timeline,21,long.MaxValue,false);
            Assert(index.Current==null,"next trial never inherits old state");
            timeline.Seek(1);index.Prepare(timeline,1,long.MaxValue,false);
            Assert(index.Current==sample.guidanceState,"backward seek restores exact state");
            Assert(TrialReviewGuidanceState.Hit(null,true)=="None"&&TrialReviewGuidanceState.Hit("old",false).Contains("invalid"),"none differs from invalid");
            using(var renderer=new TrialReplayRenderer(timeline))
            using(var overlay=new TrialReviewVicinity())
            {
                renderer.Prepare(timeline,1);
                var origin=sample.headPosition;
                var ray=new Ray(origin,Vector3.back); // Intentionally contradict the captured winner.
                overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,false,sample.guidanceState);
                Assert(overlay.RecordedWinner&&overlay.WinnerId==sample.guidanceState.vicinityWinnerObjectId,"recorded winner overrides reconstruction");
                sample.guidanceState.vicinityWinnerObjectId=null;
                overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,false,sample.guidanceState);
                Assert(overlay.WinnerId==null,"recorded no-hit must not fall back to geometry");
                sample.guidanceState.vicinityWinnerObjectId=timeline.State.Trial.targetId;
                sample.guidanceState.gazeValid=false;
                overlay.Prepare(timeline,renderer,1,long.MaxValue,true,ray,false,sample.guidanceState);
                Assert(overlay.WinnerId==null,"invalid captured gaze suppresses winner");
            }
        }
        finally {sample.guidanceState=original;timeline.Seek(1);}
        VerifySerializedAbsence();
        VerifyTransitions();
        Debug.Log("TRIAL_REVIEW_GUIDANCE_STATE_PASS legacy, validity, sequence, gaps, disconnect, seek, recorded precedence");
    }
    static void VerifySerializedAbsence()
    {
        // Both old JSON without the field and Unity's default nested placeholder
        // must remain unavailable. Invalid tracking on a real phase is different.
        var missing=JsonUtility.FromJson<TrialReplayRecord>("{\"kind\":\"sample\"}");
        var placeholder=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(new TrialReplayRecord {kind="sample"}));
        Assert(!TrialReviewGuidanceState.IsRecorded(missing.guidanceState),"missing JSON field is not a captured state");
        Assert(!TrialReviewGuidanceState.IsRecorded(placeholder.guidanceState),"serialized null placeholder is not a captured state");
        Assert(!TrialReviewGuidanceState.IsRecorded(new GuidanceStateSnapshot()),"default version is not proof of capture");
        Assert(TrialReviewGuidanceState.IsRecorded(new GuidanceStateSnapshot {phase="Suspended",gazeValid=false}),"real invalid-tracking phase remains recorded");
    }
    static void VerifyTransitions()
    {
        var turning=new GuidanceStateSnapshot {phase="Turning",revision=1};
        var refining=new GuidanceStateSnapshot {phase="Refining",previousPhase="Turning",transitionReason="wall_entry",revision=2};
        var timeline=new TrialReplayTimeline(new System.Collections.Generic.List<TrialReplayRecord> {
            new TrialReplayRecord {kind="header",sequence=0,time=0},
            new TrialReplayRecord {kind="trial",trialId="a",sequence=1,time=0,objects=Array.Empty<TrialReplayObject>()},
            new TrialReplayRecord {kind="sample",trialId="a",sequence=2,time=1,guidanceState=turning},
            new TrialReplayRecord {kind="guidance_state",trialId="a",sequence=3,time=1,guidanceState=refining},
            new TrialReplayRecord {kind="audio_start",trialId="a",sequence=4,time=1.01}
        });
        var index=new TrialReviewGuidanceState();
        timeline.Seek(1,2);index.Prepare(timeline,1,2,false);
        Assert(index.Current==turning,"same-time sample precedes transition");
        timeline.Seek(1.01);index.Prepare(timeline,1.01,long.MaxValue,false);
        Assert(index.Current==refining,"transition survives later audio event");
        timeline.Seek(1,2);index.Prepare(timeline,1,2,false);
        Assert(index.Current==turning,"backward seek restores previous phase at exact sequence");
        var tracks=new TrialReviewTracks();tracks.Refresh(timeline);
        Assert(tracks.For(timeline.Trials[0]).Events.Exists(r=>r.kind=="guidance_state"),"transition appears on event track");
        var complete=new GuidanceStateSnapshot { phase="Completed",previousPhase="Refining" };
        timeline.Records.Add(new TrialReplayRecord {kind="guidance_state",trialId="a",sequence=5,time=2,guidanceState=complete});
        timeline.Seek(2);index.Prepare(timeline,2,long.MaxValue,false);
        Assert(index.Current==complete,"completion survives absence of fresh pose");
        timeline.Seek(1,2);index.Prepare(timeline,1,2,false);
        Assert(index.Current==turning,"later completion never leaks backward");
    }
}
