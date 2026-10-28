using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TrialReviewBuild
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Build()
    {
        TrialReviewAvatarSetup.Prepare();
        Verify();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Trial Review").AddComponent<TrialReviewApp>();
        EditorSceneManager.SaveScene(scene,"Assets/TrialReview.unity");
        PlayerSettings.companyName="Research Tools"; PlayerSettings.productName="Trial Review";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone,"org.gazestudy.trialreview");
        PlayerSettings.SplashScreen.show=false;
        PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;
        PlayerSettings.iOS.microphoneUsageDescription="Record participant answers for the interview and participant-authorized voice cloning.";
        PlayerSettings.runInBackground=true;PlayerSettings.SetArchitecture(UnityEditor.Build.NamedBuildTarget.Standalone, 2);
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        // The copied replay renderer chooses this built-in fallback in the isolated project.
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
        bool found=false;for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==Shader.Find("Unlit/Color"))found=true;
        if(!found){shaders.InsertArrayElementAtIndex(shaders.arraySize);shaders.GetArrayElementAtIndex(shaders.arraySize-1).objectReferenceValue=Shader.Find("Unlit/Color");}
        graphics.ApplyModifiedProperties();AssetDatabase.SaveAssets();
        string output=Environment.GetEnvironmentVariable("TRIAL_REVIEW_OUTPUT");
        if(string.IsNullOrEmpty(output) || !output.EndsWith(".app",StringComparison.Ordinal))throw new ArgumentException("TRIAL_REVIEW_OUTPUT must be a new .app path");
        if(Directory.Exists(output))throw new IOException("Refusing to overwrite an existing app");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/TrialReview.unity"},locationPathName=output,target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Desktop build failed: "+report.summary.result);
        Debug.Log("TRIAL_REVIEW_BUILD_PASS "+output);
    }
    public static void Verify()
    {
        string folder="Assets/StreamingAssets/Demo";
        // Only generated synthetic assets in this isolated project may be replaced.
        Directory.CreateDirectory(folder);
        string source=CreateDemo(folder);
        AssetDatabase.Refresh();
        TrialReviewLiveChecks.Run(source);
        var timeline=TrialReplayFile.Read(source);
        TrialReviewVicinityChecks.Run(timeline);
        TrialReviewGuidanceStateChecks.Run(timeline);
        TrialReviewEvaluationChecks.Run();
        TrialReviewCatalogChecks.Run(timeline);
        TrialReviewGazeLineChecks.Run();
        TrialReviewInterfaceChecks.Run(timeline);
        TrialReviewCaptureChecks.Run();
        TrialReviewThetaChecks.Run(timeline);
        TrialReviewJevChecks.Run();
        Assert(timeline.Trials.Count==3 && timeline.Meshes.Count==3,"Trial/mesh reconstruction");
        using(var renderer=new TrialReplayRenderer(timeline){RecordedHead=true})
        {
            renderer.Prepare(timeline,1);
            Assert(renderer.UsingRecordedHead && renderer.Camera.transform.position==timeline.State.Sample.headPosition,"Recorded camera pose");
            var sample=timeline.State.Sample;var originalLeft=sample.leftController;
            sample.leftController=sample.rightController; // Historical recordings can have both tracked rays.
            renderer.Prepare(timeline,1);
            Assert(!renderer.LeftControllerDrawn&&renderer.RightControllerDrawn,"Only right controller ray is drawn");
            Assert(ReferenceEquals(sample.leftController,sample.rightController),"Left ray suppression preserves recorded data");
            sample.leftController=originalLeft;
            Debug.Log("TRIAL_REVIEW_RIGHT_RAY_ONLY_PASS both tracked, left hidden, right retained, data preserved");
            renderer.Prepare(timeline,4.1);Assert(!renderer.UsingRecordedHead,"Unknown head tracking must not drive first person");
            renderer.Prepare(timeline,8.2);Assert(!renderer.SampleCurrent,"Gap must not retain fresh pose");
            renderer.Prepare(timeline,6);Assert(!timeline.State.Visible,"Paused trial hides stimuli");
            renderer.Prepare(timeline,1);Assert(renderer.UsingRecordedHead && timeline.State.Selection==null,"Backward seek resets state");
            renderer.Prepare(timeline,21);Assert(timeline.State.Trial.trialId=="demo-1","Next trial");
            renderer.Prepare(timeline,1);Assert(timeline.State.Trial.trialId=="demo-0","Previous trial");
        }
        using(var actors=new TrialReviewActors())
        {
            timeline.Seek(1);actors.Prepare(timeline,1,false,true,true);
            Assert(actors.BodyVisible && actors.RightVisible && !actors.LeftVisible,"Avatar and valid controller visibility");
            var trialOrigin=timeline.State.Trial.origin;
            Assert(Mathf.Abs(actors.SeatCenter.x-trialOrigin.x)<.0001f&&Mathf.Abs(actors.SeatCenter.z-trialOrigin.z)<.0001f,"Chair is fixed at layout center");
            var sample=timeline.State.Sample;var headPosition=sample.headPosition;var headRotation=sample.headRotation;
            var seat=actors.SeatCenter;var focus=actors.ViewCenter;
            sample.headPosition+=new Vector3(1,.25f,-2);sample.headRotation=Quaternion.Euler(30,110,0);
            actors.Prepare(timeline,1,false,true,true);
            Assert(Vector3.Distance(seat,actors.SeatCenter)<.0001f&&actors.ViewCenter==focus,"Head translation and rotation cannot move the seat or orbit center");
            Assert(sample.headPosition==headPosition+new Vector3(1,.25f,-2),"Recorded head translation remains untouched");
            sample.headPosition=headPosition;sample.headRotation=headRotation;
            actors.Prepare(timeline,1,false,true,true);
            Assert(Quaternion.Angle(actors.HeadAnchor.rotation,timeline.State.Sample.headRotation)<.01f,"Avatar head orientation");
            Assert(actors.RightController.position==timeline.State.Sample.rightController.position && actors.RightController.rotation==timeline.State.Sample.rightController.rotation,"Controller uses recorded pose");
            timeline.Seek(21);actors.Prepare(timeline,21,false,true,true);
            timeline.Seek(1);actors.Prepare(timeline,1,false,true,true);
            Assert(Vector3.Distance(seat,actors.SeatCenter)<.0001f&&actors.ViewCenter==focus,"Seat remains stable after trial switch and rewind");
            actors.Prepare(timeline,1,true,true,true);Assert(!actors.BodyVisible && actors.RightVisible,"First person hides body, retains tracked controller");
            timeline.Seek(8.2);actors.Prepare(timeline,8.2,false,true,true);Assert(!actors.BodyVisible&&!actors.RightVisible&&!actors.LeftVisible,"No model poses across gaps");
            timeline.Seek(1);var controller=timeline.State.Sample.rightController;controller.tracked=0;
            actors.Prepare(timeline,1,false,true,true);Assert(!actors.RightVisible,"Untracked controller hidden");controller.tracked=1;
            actors.Prepare(timeline,1,false,false,false);Assert(!actors.BodyVisible&&!actors.RightVisible,"Model toggles independent");
        }
        VerifyGazeCorrectionMath(timeline);
        VerifyQuickAlignmentReplay();
        VerifyReadinessEyeRay();
        VerifyEnvironment(timeline);
        VerifyArmAndEye(timeline);
        VerifyRestingLeftArm();
        VerifyTorsoEstimate();
        Debug.Log("TRIAL_REVIEW_ACTORS_PASS body anchor/rotation, controller pose, first person, gaps, tracking and toggles");
        var first=timeline.Records.Find(r=>r.kind=="sample"&&r.time==1);
        timeline.Seek(1,first.sequence);Assert(timeline.State.Sample.sequence==first.sequence,"Exact sequence cursor");
        var output=new SilentOutput();
        using(var audio=new TrialReplayAudioPreview(timeline,output))
        {
            audio.Sync(1.2,true,1);Assert(audio.ActiveCount==1,"Saved audio begins");
            audio.Sync(1.5,true,1);Assert(audio.ActiveCount==0,"Saved cancellation stops audio");
            audio.Sync(1.2,true,1,long.MaxValue,true);Assert(audio.ActiveCount==1,"Seek restores audio");
            audio.Sync(1.2,true,2);Assert(audio.ActiveCount==0,"Non-1x muted");
        }
        var csv=TrialReviewSummary.Parse("a,b\r\n\"one, two\",\"a\"\"b\nline\"\r\n");
        Assert(csv.Count==2 && csv[1][0]=="one, two"&&csv[1][1]=="a\"b\nline","CSV quoted values");
        string hash=TrialReviewSummary.Hash(source);
        File.WriteAllText(Path.Combine(folder,"manifest.json"),"{\"schema\":\"trial-performance-subjective-v2\",\"inputs\":[{\"sha256\":\""+hash+"\"}]}");
        File.WriteAllText(Path.Combine(folder,"trials.csv"),"replay_recording_id,trial_id,participant_id,voice_condition,is_practice,run_number,block_index,time_to_final_selection_seconds,nasa_tlx_total_mean,outcome\n"+
            string.Join("\n",Enumerable.Range(0,3).Select(i=>$"synthetic-desktop-demo,SYNTHETIC_trial_{i},SYNTHETIC,neutral,0,1,0,14,,completed"))+"\n");
        var summary=new TrialReviewSummary(folder,timeline,hash);
        Assert(summary.For(timeline.Trials[0])["time_to_final_selection_seconds"]=="14","Exact summary join");
        bool rejected=false;try{new TrialReviewSummary(folder,timeline,"wronghash");}catch(InvalidDataException){rejected=true;}
        Assert(rejected,"Reject wrong recording export");
        string real = Environment.GetEnvironmentVariable("TRIAL_REVIEW_VERIFY_RECORDING");
        if (!string.IsNullOrEmpty(real))
        {
            var saved = TrialReplayFile.Read(real);
            int heads=0;
            using(var renderer=new TrialReplayRenderer(saved){RecordedHead=true})
                foreach(var sample in saved.Records.Where(r=>r.kind=="sample" && r.headAvailable && r.headTracked==1).Where((r,i)=>i%100==0))
                {
                    renderer.Prepare(saved,sample.time,maxSequence:sample.sequence);
                    Assert(renderer.UsingRecordedHead && renderer.Camera.transform.position==sample.headPosition,"Real recording camera pose"); heads++;
                }
            Assert(heads>0,"Real recording has valid head samples");
            VerifyRealGazeCorrection(saved,real);
            var charging=saved.Records.FirstOrDefault(r=>r.kind=="sample"&&r.readinessActive&&!r.readinessPaused&&r.readinessProgress<1&&r.headTracked==1&&r.gazeTracked==1);
            if(charging!=null)using(var actors=new TrialReviewActors())
            {
                saved.Seek(charging.time,charging.sequence);actors.Prepare(saved,charging.time,false,true,true,true);
                Assert(actors.EyeRay.enabled,"Real recording charging gaze visible");
                Debug.Log("TRIAL_REVIEW_REAL_READINESS_PASS time="+charging.time+" charge="+charging.readinessProgress);
            }
            string export=Environment.GetEnvironmentVariable("TRIAL_REVIEW_VERIFY_EXPORT");
            if(!string.IsNullOrEmpty(export))
            {
                var joined=new TrialReviewSummary(export,saved,TrialReviewSummary.Hash(real));
                Assert(saved.Trials.Any(t=>joined.For(t)!=null),"Real recording summary joins");
            }
            Debug.Log("TRIAL_REVIEW_REAL_PASS "+heads+" sampled camera poses; "+saved.Trials.Count+" trials; warning="+saved.Warning);
        }
        Debug.Log("TRIAL_REVIEW_VERIFY_PASS camera, gaps, pause, rewind, trial switch, sequence, audio, CSV and identity");
    }
    static void VerifyGazeCorrectionMath(TrialReplayTimeline timeline)
    {
        var origin=new Vector3(1,2,3);var gaze=new Vector3(4,5,6);
        Assert(Vector3.Distance(TrialReviewGazeCorrection.UnmirrorOrigin(gaze,origin,Quaternion.identity),new Vector3(-2,5,6))<.00001f,"Translated reflection");
        var rotated=Quaternion.Euler(0,90,0);
        var corrected=TrialReviewGazeCorrection.UnmirrorOrigin(gaze,origin,rotated);
        Assert(Vector3.Distance(corrected,new Vector3(4,5,0))<.00001f,"Rotated tracking reference reflection");
        Assert(Vector3.Distance(TrialReviewGazeCorrection.UnmirrorOrigin(corrected,origin,rotated),gaze)<.00001f,"Reflection round trip");
        var unknown=new TrialReviewGazeCorrection(timeline,"unknown");
        Assert(!unknown.Supported&&!unknown.TryGet(timeline.Records.First(r=>r.kind=="sample"),out _),"Unknown recording never corrected");
        Debug.Log("TRIAL_REVIEW_CORRECTION_MATH_PASS translation, rotation, round trip, unknown-profile rejection");
    }
    static void VerifyRealGazeCorrection(TrialReplayTimeline timeline,string path)
    {
        string hash=TrialReviewSummary.Hash(path);
        var correction=new TrialReviewGazeCorrection(timeline,hash);
        if(!correction.Supported)return; // Generic build verifier also supports unrelated recordings.
        Assert(!new TrialReviewGazeCorrection(timeline,"wrong-hash").Supported,"Correction requires exact source hash");
        int count=0;float min=float.PositiveInfinity,max=0;
        using(var actors=new TrialReviewActors())
        {
            foreach(var sample in timeline.Records.Where(r=>r.kind=="sample"))
            {
                string before=JsonUtility.ToJson(sample);
                bool applied=correction.TryGet(sample,out var ray);
                if(sample.gazeTracked!=1)Assert(!applied,"Invalid raw gaze remains a gap");
                if(applied&&sample.headAvailable&&sample.headTracked==1)
                {
                    float residual=Vector3.Distance(ray.origin,sample.headPosition);min=Mathf.Min(min,residual);max=Mathf.Max(max,residual);count++;
                    Assert(Vector3.Angle(ray.direction,sample.gazeDirection)<.04f,"Historical position-only fix preserves direction");
                    if(count==1)
                    {
                        timeline.Seek(sample.time,sample.sequence);actors.Prepare(timeline,sample.time,false,true,true,true,correction);
                        Assert(actors.CorrectionApplied&&actors.EyeRay.enabled,"Corrected readiness ray shown");
                        Assert(Vector3.Distance(actors.GazeRay.origin,ray.origin)<.00001f,"Analysis uses derived origin, not cosmetic headset position");
                        Assert(Vector3.Distance(actors.EyeRay.GetPosition(0),actors.HeadsetRayOrigin)<.00001f,"Third-person connector uses headset face");
                        Assert(Vector3.Distance(ray.origin,sample.headPosition)>.02f,"Genuine eye-head offset preserved");
                        bool reference=sample.xrOriginAvailable;sample.xrOriginAvailable=false;
                        actors.Prepare(timeline,sample.time,false,true,true,true,correction);
                        Assert(!actors.EyeRay.enabled&&!correction.TryGet(sample,out _),"Missing reference does not fallback to cosmetic ray");
                        sample.xrOriginAvailable=reference;
                        var rotation=sample.xrOriginRotation;sample.xrOriginRotation=new Quaternion(0,0,0,0);
                        Assert(!correction.TryGet(sample,out _),"Invalid reference quaternion rejected");sample.xrOriginRotation=rotation;
                    }
                }
                Assert(before==JsonUtility.ToJson(sample),"Correction never changes sample fields");
            }
        }
        // These are regression bounds for this exact reviewed file, not generic physiological thresholds.
        Assert(count==8134&&min>.022f&&max<.030f,"P020 reference residual regression");
        Assert(TrialReviewSummary.Hash(path)==hash,"Original file hash unchanged");
        Debug.Log($"TRIAL_REVIEW_P020_CORRECTION_PASS {count} tracked samples; head residual {min:F6}–{max:F6} m; raw preserved");
    }
    static void VerifyQuickAlignmentReplay()
    {
        var alignment = new GazeQuickAlignment();
        double clock = 10;
        Vector3 head = new Vector3(1, 1.6f, -2);
        Quaternion rotation = Quaternion.Euler(0, 50, 0);
        GazeObservation Observation(Vector3 target)
        {
            Vector3 origin = head + rotation * new Vector3(.015f, 0, .02f);
            var expected = Quaternion.Inverse(rotation) * (target-origin).normalized;
            float az = Mathf.Atan2(expected.x, expected.z) + 2 * Mathf.Deg2Rad;
            float el = Mathf.Asin(expected.y) + Mathf.Deg2Rad;
            var measured = rotation * new Vector3(Mathf.Cos(el)*Mathf.Sin(az),Mathf.Sin(el),Mathf.Cos(el)*Mathf.Cos(az));
            return new GazeObservation { valid=true,deviceAvailable=true,headValid=true,focused=true,newInputUpdate=true,
                inputEventTime=clock,receiptTime=clock,sequence=(long)(clock*1000),recordingId="synthetic-quick-eye",
                participant="synthetic",deviceId=7,raySource="HTC_binocular_mean",worldOrigin=origin,worldDirection=measured,
                unalignedWorldOrigin=origin,unalignedWorldDirection=measured,headPosition=head,headRotation=rotation,
                rawWorldDirection=Vector3.back }; // Rejected EXT must not replace the usable native source.
        }
        alignment.Begin(head,rotation,clock);
        for(int i=0;i<600&&alignment.IsRunning;i++) { clock+=.02;alignment.Observe(Observation(alignment.CurrentTarget)); }
        Assert(alignment.IsActive,"Replay fixture fits and validates live quick alignment");
        rotation=Quaternion.Euler(-8,-70,3); // A different head orientation after fitting.
        var observation=Observation(head+rotation*Vector3.forward*3);
        var originalRaw=observation.rawWorldDirection;
        alignment.Apply(observation);
        var originFrame=new Vector3(3,.1f,4);var rotationFrame=Quaternion.Euler(0,35,0);
        var inverse=Quaternion.Inverse(rotationFrame);
        var row=new TrialReplayRecord {kind="sample",time=1,sequence=2,trialId="quick",headAvailable=true,headTracked=1,
            headPosition=inverse*(head-originFrame),headRotation=inverse*rotation,readinessActive=true};
        TrialReplayGaze.Capture(row,observation,true,originFrame,rotationFrame);
        var saved=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(row));
        Assert(saved.gazeAlignmentApplied&&saved.gazeAlignmentId==alignment.Report.id&&saved.gazeRaySource=="HTC_binocular_mean",
            "Alignment provenance survives capture and JSON round trip");
        Assert(Vector3.Angle(rotationFrame*saved.gazeDirection,observation.worldDirection)<.04f &&
            Vector3.Distance(originFrame+rotationFrame*saved.gazeOrigin,observation.worldOrigin)<.00001f,
            "Replay reconstructs exactly the live used ray in a rotated recording frame");
        Assert(Vector3.Angle(saved.gazeDirection,saved.gazeUnalignedDirection)>1.5f&&
            observation.rawWorldDirection==originalRaw,"Pre-alignment and raw EXT measurements are preserved independently");
        var cleared=JsonUtility.FromJson<TrialReplayRecord>(JsonUtility.ToJson(saved));
        cleared.time=1.05;cleared.sequence=3;cleared.gazeAlignmentApplied=false;cleared.gazeAlignmentStatus="cleared";
        cleared.gazeDirection=cleared.gazeUnalignedDirection;
        var rows=new List<TrialReplayRecord>{new TrialReplayRecord{kind="header",sequence=0,gazeRayVersion=TrialReplayGaze.Version},
            new TrialReplayRecord{kind="trial",trialId="quick",sequence=1,objects=Array.Empty<TrialReplayObject>()},saved,cleared};
        var timeline=new TrialReplayTimeline(rows);
        using(var actors=new TrialReviewActors())
        using(var renderer=new TrialReplayRenderer(timeline){RecordedHead=true,ShowGaze=true})
        {
            foreach(double time in new[]{1.0,1.05,1.0})
            {
                timeline.Seek(time);actors.Prepare(timeline,time,false,true,true,true);
                var current=timeline.State.Sample;
                Assert(actors.EyeRay.enabled&&Vector3.Distance(actors.GazeRay.origin,current.gazeOrigin)<.00001f,
                    "Analytical replay keeps measured eye origin independent of headset connector");
                Assert(Vector3.Angle(actors.GazeRay.direction,current.gazeDirection)<.04f,
                    "Seeking never double-applies quick correction");
                renderer.ShowUnalignedGaze=true;renderer.Prepare(timeline,time);
                var ray=(LineRenderer)typeof(TrialReplayRenderer).GetField("m_Ray",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(renderer);
                Assert(renderer.GazeDrawn&&Vector3.Angle(ray.GetPosition(1)-ray.GetPosition(0),current.gazeUnalignedDirection)<.04f,
                    "Before-alignment toggle renders the actual saved pre-correction direction");
                Assert(renderer.UsingRecordedHead,"First-person camera still follows saved head pose");
            }
            timeline.Seek(2);actors.Prepare(timeline,2,false,true,true,true);renderer.Prepare(timeline,2);
            Assert(!actors.EyeRay.enabled&&!renderer.GazeDrawn,"Replay never holds aligned or unaligned gaze across gaps");
        }
        var invalid=new TrialReplayRecord();observation.worldDirection=new Vector3(float.NaN,0,0);
        TrialReplayGaze.Capture(invalid,observation,false,originFrame,rotationFrame);
        Assert(invalid.gazeTracked==0&&!invalid.gazeUnalignedAvailable&&!invalid.gazeAlignmentApplied&&invalid.gazeDirection==Vector3.zero,
            "Invalid recorded gaze stays a gap with no nonfinite geometry");
        Debug.Log("TRIAL_REVIEW_QUICK_ALIGNMENT_PASS live fit -> capture -> JSON -> first/third person, exact origin, pre-alignment toggle, backwards seek, clearing and gaps");
    }
    static void VerifyReadinessEyeRay()
    {
        var rows=new List<TrialReplayRecord>{
            new TrialReplayRecord{kind="header",sequence=0},
            new TrialReplayRecord{kind="trial",trialId="ready",sequence=1,objects=Array.Empty<TrialReplayObject>()}};
        for(int i=0;i<5;i++)rows.Add(new TrialReplayRecord{kind="sample",trialId="ready",sequence=i+2,time=i/20.0,
            headAvailable=true,headTracked=1,headPosition=new Vector3(0,1.5f,0),
            gazeAvailable=true,gazeTracked=i==3?0:1,gazeDirection=Vector3.forward,
            readinessActive=true,readinessProgress=i*.2f,readinessPaused=i==2});
        rows.Add(new TrialReplayRecord{kind="readiness_paused",trialId="ready",sequence=7,time=.22,readinessActive=true,readinessPaused=true});
        rows.Add(new TrialReplayRecord{kind="transition",trialId="ready",sequence=8,time=.25});
        var timeline=new TrialReplayTimeline(rows);
        using(var actors=new TrialReviewActors())
        {
            Action<double,bool,string> check=(time,expected,reason)=>{timeline.Seek(time);actors.Prepare(timeline,time,false,true,true,true);Assert(actors.EyeRay.enabled==expected,reason);};
            check(0,true,"Zero-charge gaze shown before objects");
            Assert(!timeline.State.Visible&&!timeline.State.Searching,"Readiness fixture hides objects");
            check(.05,true,"Partial-charge gaze shown");
            check(.10,false,"Paused readiness hides gaze");
            check(.15,false,"Invalid gaze during readiness hidden");
            check(.20,true,"Tracking recovery restores readiness gaze");
            actors.Prepare(timeline,.20,false,true,true,false);Assert(!actors.EyeRay.enabled,"Eye toggle still respected in readiness");
            check(.22,false,"Readiness pause event overrides earlier active sample");
            check(.25,false,"Transition clears readiness gaze");
            check(.05,true,"Backward seek restores charging gaze");
            actors.Prepare(timeline,.19,false,true,true,true);Assert(!actors.EyeRay.enabled,"Stale readiness and gaze hidden");
        }
        Debug.Log("TRIAL_REVIEW_READINESS_EYE_PASS zero/partial charge, paused/invalid/stale tracking, recovery, transitions, toggle and rewind");
    }
    static void VerifyEnvironment(TrialReplayTimeline timeline)
    {
        var trial=timeline.Trials[0];string original=JsonUtility.ToJson(trial);
        using(var environment=new TrialReviewEnvironment())
        {
            environment.Prepare(trial,true,true);
            Assert(environment.WallCount==8&&environment.WallsVisible&&environment.RoomVisible,"Eight inferred walls and room");
            Assert(Mathf.Abs(environment.Radius-3)<.001f,"Wall radius comes from snapshot objects");
            float floor=environment.Floor;
            float legacyHeight=environment.WallHeight;
            Assert(!environment.HasRecordedWallBounds,"Legacy records remain inferred, never globally rescaled");
            var compact=JsonUtility.FromJson<TrialReplayRecord>(original);
            compact.hasSearchPlaneBounds=true;compact.searchPlaneBottom=-1.2f;compact.searchPlaneHeight=2.2036f;
            foreach(var obj in compact.objects)obj.position=new Vector3(obj.position.x,-1.1f+(obj.position.y-floor)*.5f,obj.position.z);
            string compactBefore=JsonUtility.ToJson(compact);
            environment.Prepare(compact,true,true);
            Assert(environment.HasRecordedWallBounds&&Mathf.Abs(environment.Floor+1.2f)<.0001f&&
                Mathf.Abs(environment.WallHeight-2.2036f)<.0001f,"Exact compact bounds take precedence over inferred object extents");
            Assert(JsonUtility.ToJson(compact)==compactBefore,"Compact playback does not rescale recorded objects");
            Vector3 inside=compact.origin+compact.originRotation*new Vector3(0,1f,0);
            Vector3 above=compact.origin+compact.originRotation*new Vector3(0,1.1f,0);
            Vector3 forward=compact.originRotation*Vector3.forward;
            Assert(environment.TryProjectGaze(new Ray(inside,forward),out _)&&
                !environment.TryProjectGaze(new Ray(above,forward),out _),"Projection respects the recorded shorter wall top");
            environment.Prepare(trial,true,true);
            Assert(!environment.HasRecordedWallBounds&&Mathf.Abs(environment.WallHeight-legacyHeight)<.0001f,
                "Switching back to old trials restores historical bounds");
            for(int wall=0;wall<8;wall++)
            {
                Vector3 origin=trial.origin+trial.originRotation*new Vector3(0,floor+.5f,0);
                Vector3 direction=trial.originRotation*(Quaternion.Euler(0,wall*45,0)*Vector3.forward);
                Assert(environment.TryProjectGaze(new Ray(origin,direction),out var hit)&&
                    Vector3.Distance(hit,origin+direction*3)<.001f,"Gaze dot intersects each wall at its actual plane distance");
            }
            Assert(!environment.TryProjectGaze(new Ray(trial.origin+Vector3.up*100,Vector3.forward),out _),"No dot outside finite wall height");
            Assert(!environment.TryProjectGaze(new Ray(trial.origin,Vector3.up),out _),"Vertical ray cannot invent wall intersection");
            environment.Prepare(trial,false,true);Assert(!environment.WallsVisible&&environment.RoomVisible,"Independent wall toggle");
            environment.Prepare(trial,true,false);Assert(environment.WallsVisible&&!environment.RoomVisible,"Independent room toggle");
            environment.Prepare(timeline.Trials[1],true,true);environment.Prepare(trial,true,true);
            Assert(environment.WallCount==8&&Mathf.Abs(environment.Floor-floor)<.001f,"Trial-switch and rewind rebuild");
            var moved=JsonUtility.FromJson<TrialReplayRecord>(original);moved.origin=new Vector3(10,2,-4);moved.originRotation=Quaternion.Euler(0,75,0);
            foreach(var o in moved.objects)o.position=moved.origin+moved.originRotation*o.position;
            environment.Prepare(moved,true,true);Assert(environment.Center==moved.origin&&Mathf.Abs(environment.Radius-3)<.001f,"Snapshot origin/rotation respected");
            Vector3 dotOrigin=moved.origin+moved.originRotation*new Vector3(0,environment.Floor+.5f,0);
            Vector3 dotDirection=moved.originRotation*Vector3.forward;
            Assert(environment.TryProjectGaze(new Ray(dotOrigin,dotDirection),out var dot)&&
                Vector3.Distance(dot,dotOrigin+dotDirection*3)<.001f,"Gaze dot respects translated and rotated study frame");
            Assert(environment.DegreePositions.Length==8,"Eight degree labels");
            for(int plane=0;plane<8;plane++)
                Assert(Vector3.Distance(environment.DegreePositions[plane],moved.origin+moved.originRotation*(Quaternion.Euler(0,plane*45,0)*new Vector3(0,.43f,3)))<.001f,"Degree label follows saved layout rotation");
            environment.Prepare(new TrialReplayRecord{objects=Array.Empty<TrialReplayObject>()},true,true);
            Assert(!environment.WallsVisible&&!environment.RoomVisible,"No stale environment for absent layout");
            Assert(environment.DegreePositions.Length==0,"Missing layout clears degree labels");
            environment.Prepare(null,true,true);Assert(!environment.RoomVisible,"Null trial safe");
            var unknown=JsonUtility.FromJson<TrialReplayRecord>(original);unknown.layout="unknown";
            environment.Prepare(unknown,true,true);Assert(!environment.WallsVisible&&environment.RoomVisible,"Do not invent octagonal walls for unknown layouts");
            Assert(!environment.TryProjectGaze(new Ray(Vector3.zero,Vector3.forward),out _),"Unknown layout hides gaze dot");
        }
        Assert(JsonUtility.ToJson(trial)==original,"Environment leaves recorded snapshot unchanged");
        Debug.Log("TRIAL_REVIEW_ENVIRONMENT_PASS toggles, eight walls, layout radius, origin, rotation, trial switch, rewind, unknown layout, no data mutation");
    }
    static void VerifyArmAndEye(TrialReplayTimeline timeline)
    {
        timeline.Seek(1);
        var sample=timeline.State.Sample;
        string original=JsonUtility.ToJson(sample);
        using(var actors=new TrialReviewActors())
        {
            actors.Prepare(timeline,1,false,true,true,true);
            Assert(actors.RightArm.Following&&!actors.RightArm.ReachLimited,"Ordinary controller reach solved");
            Assert(Vector3.Distance(actors.RightArm.Palm.position,actors.RightGrip.position)<.001f,"Hand meets controller grip");
            Assert(Quaternion.Angle(actors.RightArm.Palm.rotation,actors.RightGrip.rotation)<.05f,"Hand follows controller orientation");
            using(var rays=new TrialReplayRenderer(timeline))
            {
                rays.ShowControllers=true;rays.Prepare(timeline,1);
                var recordedEnd=rays.RightControllerRayEnd;
                rays.AttachControllerRay(actors.RightControllerTip);
                Assert(Vector3.Distance(rays.RightControllerRayStart,actors.RightControllerTip)<.0001f,"Display ray starts at controller tip");
                Assert(rays.RightControllerRayEnd==recordedEnd&&recordedEnd==sample.rightController.rayEnd,"Tip attachment preserves recorded hit endpoint");
                rays.ShowControllers=false;rays.Prepare(timeline,1);rays.AttachControllerRay(actors.RightControllerTip);
                Assert(!rays.RightControllerDrawn,"Tip attachment cannot reveal a hidden controller ray");
            }
            var palm=actors.RightArm.Palm.position;
            var controllerRotation=sample.rightController.rotation;
            var thumb=actors.RightArm.Palm.parent.GetComponentsInChildren<Transform>().Single(t=>t.name=="RightHandThumb2_end");
            var thumbInController=actors.RightController.InverseTransformPoint(thumb.position);
            foreach(var offset in new[]{Quaternion.Euler(25,0,0),Quaternion.Euler(0,45,20),Quaternion.Euler(-20,-35,-15)})
            {
                sample.rightController.rotation=controllerRotation*offset;
                actors.Prepare(timeline,1,false,true,true,true);
                Assert(Vector3.Distance(actors.RightArm.Palm.position,actors.RightGrip.position)<.001f,"Raised and rotated controller retains hand contact");
                Assert(Vector3.Distance(actors.RightController.InverseTransformPoint(thumb.position),thumbInController)<.001f,"Thumb stays on controls through controller rotation");
                Assert(actors.RightController.rotation==sample.rightController.rotation,"Grip fitting cannot rotate recorded controller");
            }
            sample.rightController.rotation=controllerRotation;
            actors.Prepare(timeline,1,false,true,true,true);
            var savedOrigin=sample.gazeOrigin;
            sample.gazeOrigin+=Vector3.right*2;
            actors.Prepare(timeline,1,false,true,true,true);
            Assert(actors.EyeRay.enabled&&Vector3.Distance(actors.EyeRay.GetPosition(0),actors.HeadsetRayOrigin)<.0001f,"Third-person display ray starts on headset face");
            Assert(Vector3.Distance(actors.GazeRay.origin,sample.headPosition)<.0001f&&Vector3.Angle(actors.GazeRay.direction,sample.gazeDirection)<.01f,"Analytical legacy ray preserves head origin and world direction");
            Assert(Vector3.Distance(actors.EyeRay.GetPosition(1),actors.GazeRay.origin+actors.GazeRay.direction*3)<.0001f,"Headset connector preserves analytical endpoint");
            Assert(Mathf.Abs(actors.GazeOriginOffset-2)<.001f&&sample.gazeOrigin==savedOrigin+Vector3.right*2,"Raw offset disclosed and preserved");
            sample.gazeOrigin=savedOrigin;
            int tracked=sample.gazeTracked;sample.gazeTracked=0;actors.Prepare(timeline,1,false,true,true,true);
            Assert(!actors.EyeRay.enabled,"Invalid gaze hides eye ray");sample.gazeTracked=tracked;
            timeline.Seek(8.2);actors.Prepare(timeline,8.2,false,true,true,true);
            Assert(!actors.EyeRay.enabled&&!actors.RightArm.Following,"Gap resets arm and eye ray");
            timeline.Seek(1);actors.Prepare(timeline,1,false,true,true,true);
            Assert(Vector3.Distance(palm,actors.RightArm.Palm.position)<.0001f,"Arm seek determinism");
            actors.Prepare(timeline,1,true,true,true,true);
            Assert(!actors.BodyVisible&&actors.EyeRay.enabled,"First-person eye ray without visible body");
            Assert(Vector3.Distance(actors.EyeRay.GetPosition(0),actors.GazeRay.origin)<.0001f,"First-person gaze projection keeps analytical origin");
            var position=sample.rightController.position;sample.rightController.position+=Vector3.up*10;
            actors.Prepare(timeline,1,false,true,true,true);
            Assert(actors.RightArm.ReachLimited&&actors.RightController.position==sample.rightController.position,"Unreachable poses disclosed without moving controller");
            sample.rightController.position=position;
            var left=sample.leftController;sample.leftController=new TrialReplayController{available=true,tracked=1,position=new Vector3(-.22f,1.15f,.1f),rotation=Quaternion.Euler(20,30,10)};
            actors.Prepare(timeline,1,false,true,true,true);
            Assert(!actors.LeftArm.Following&&!actors.LeftVisible,"Unused left controller cannot drive the arm or appear detached");
            sample.leftController=left;
            sample.rightController.tracked=0;actors.Prepare(timeline,1,false,true,true,true);
            Assert(!actors.RightArm.Following,"Untracked hand returns to neutral pose");sample.rightController.tracked=1;
            timeline.Seek(6);actors.Prepare(timeline,6,false,true,true,true);Assert(!actors.EyeRay.enabled,"Paused trial hides eye ray");
        }
        Assert(JsonUtility.ToJson(sample)==original,"Presentation leaves sample data unchanged");
        VerifyForwardElbow();
        Debug.Log("TRIAL_REVIEW_HAND_EYE_PASS right grip position/orientation, resting left arm, bounds, rewind, tracking, gaps, eye origin/direction, raw preservation");
    }
    static void VerifyForwardElbow()
    {
        var body=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ReplayParticipant"));
        var controller=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("ReplayControllerR"));
        try
        {
            var arm=new TrialReviewArm(body,"Right");
            var bones=body.GetComponentsInChildren<Transform>(true);
            var elbow=bones.Single(t=>t.name=="RightForeArm");
            var wrist=bones.Single(t=>t.name=="RightHand");
            foreach(float yaw in new[]{0f,90f,180f,0f})
            foreach(var local in new[]{new Vector3(.23f,.91f,.19f),new Vector3(.3f,1.18f,.48f),new Vector3(.39f,1.04f,.38f),new Vector3(0,1.102f,.296f)})
            {
                body.transform.rotation=Quaternion.Euler(0,yaw,0);
                controller.transform.SetPositionAndRotation(body.transform.TransformPoint(local),body.transform.rotation);
                arm.Prepare(controller.transform.Find("GripAnchor"),body.transform);
                Assert(Vector3.Dot(wrist.position-elbow.position,body.transform.forward)>0,"Close and raised controller poses bend forearm forward through torso turns and rewind");
                Assert(Vector3.Distance(arm.Palm.position,controller.transform.Find("GripAnchor").position)<.001f,"Forward elbow preserves controller contact");
                if(local.x==0)
                    Assert(body.transform.InverseTransformPoint(elbow.position).x>.15f,"Chest-center controller keeps elbow outside the torso (P020 regression)");
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(body);UnityEngine.Object.DestroyImmediate(controller);}
        Debug.Log("TRIAL_REVIEW_FORWARD_ELBOW_PASS close chest, raised, lateral, torso turns, rewind, grip contact");
    }
    static void VerifyRestingLeftArm()
    {
        var left=new TrialReplayController{available=true,tracked=1,position=new Vector3(-3,1,2),rotation=Quaternion.identity};
        string original=JsonUtility.ToJson(left);
        var rows=new List<TrialReplayRecord>{
            new TrialReplayRecord{kind="header",sequence=0},
            new TrialReplayRecord{kind="trial",sequence=1,trialId="resting-left",objects=Array.Empty<TrialReplayObject>()},
            new TrialReplayRecord{kind="search_start",sequence=2,trialId="resting-left"}};
        for(int i=0;i<3;i++)rows.Add(new TrialReplayRecord{kind="sample",sequence=i+3,time=i,trialId="resting-left",
            headAvailable=true,headTracked=1,headPosition=new Vector3(0,1.5f,0),headRotation=Quaternion.Euler(0,i*90,0),leftController=left});
        var timeline=new TrialReplayTimeline(rows);
        using(var actors=new TrialReviewActors())
        {
            timeline.Seek(0);actors.Prepare(timeline,0,false,true,true);
            var palm=actors.LeftArm.Palm;var body=palm.root;
            var lap=body.Find("LeftLapRestAnchor");
            Assert(lap!=null&&Vector3.Distance(palm.position,lap.position)<.001f,"Left fist rests at the lap anchor");
            var restingPosition=body.InverseTransformPoint(palm.position);
            var restingRotation=Quaternion.Inverse(body.rotation)*palm.rotation;
            foreach(double time in new double[]{1,2,0})
            {
                timeline.Seek(time);actors.Prepare(timeline,time,false,true,true);
                Assert(actors.BodyVisible&&!actors.LeftArm.Following&&!actors.LeftVisible,"Left arm remains body-owned during turns and rewind");
                Assert(Mathf.Abs(Mathf.DeltaAngle(actors.TorsoYaw,(float)time*90))<.01f,"Fixture actually turns the body");
                Assert(Vector3.Distance(body.InverseTransformPoint(palm.position),restingPosition)<.0001f&&
                    Quaternion.Angle(Quaternion.Inverse(body.rotation)*palm.rotation,restingRotation)<.01f,"Left arm rotates with body without world-space anchoring");
                Assert(Vector3.Distance(palm.position,lap.position)<.001f,"Left fist stays on the lap through turns and rewind");
            }
        }
        Assert(JsonUtility.ToJson(left)==original,"Unused left-controller recording remains unchanged");
        Debug.Log("TRIAL_REVIEW_RESTING_LEFT_ARM_PASS quarter/half turns, rewind, tracked unused controller, raw preservation");
    }
    static void VerifyTorsoEstimate()
    {
        var rows=new List<TrialReplayRecord>{
            new TrialReplayRecord{kind="header",sequence=0},
            new TrialReplayRecord{kind="trial",sequence=1,trialId="t",objects=Array.Empty<TrialReplayObject>()},
            new TrialReplayRecord{kind="search_start",sequence=2,trialId="t"}};
        for(int i=0;i<=20;i++)rows.Add(new TrialReplayRecord{kind="sample",sequence=i+3,time=i*.05,trialId="t",headAvailable=true,headTracked=1,
            headRotation=Quaternion.Euler(0,i<5?i*5:90,0)});
        var timeline=new TrialReplayTimeline(rows);var torso=new TrialReviewTorsoTrack(timeline);
        Assert(torso.TryGet(rows[7],out float small)&&Mathf.Abs(small)<.001f,"Small head turns leave torso and chair still");
        Assert(torso.TryGet(rows[rows.Count-1],out float larger)&&larger>0&&larger<90,"Large head turns move torso with neck allowance");
        torso.TryGet(rows[8],out float initialTurn);Assert(initialTurn<=4.501f,"Torso turn rate is bounded");
        using(var actors=new TrialReviewActors())
        {
            var last=rows[rows.Count-1];timeline.Seek(last.time,last.sequence);actors.Prepare(timeline,last.time,false,true,false);
            float before=actors.TorsoYaw;timeline.Seek(.1);actors.Prepare(timeline,.1,false,true,false);
            timeline.Seek(last.time,last.sequence);actors.Prepare(timeline,last.time,false,true,false);
            Assert(Mathf.Abs(Mathf.DeltaAngle(before,actors.TorsoYaw))<.001f,"Torso pose independent of seek order");
            Assert(Quaternion.Angle(actors.HeadAnchor.rotation,last.headRotation)<.01f,"Head remains independently tracked");
        }
        Debug.Log("TRIAL_REVIEW_TORSO_PASS head-only turns, coupled torso/seat, bounded turn rate, deterministic rewind");
    }
    sealed class SilentOutput:TrialReplayAudioPreview.IOutput
    {
        public object Play(float[] samples,int channels,int rate,int offsetFrames,string name)=>new object();
        public void Stop(object handle){}
    }
    static string CreateDemo(string folder)
    {
        Directory.CreateDirectory(Path.Combine(folder,"audio"));
        var meshes=new List<TrialReplayMesh>();
        foreach(var type in new[]{PrimitiveType.Cube,PrimitiveType.Sphere,PrimitiveType.Capsule})
        {
            var go=GameObject.CreatePrimitive(type);var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            meshes.Add(new TrialReplayMesh{id=type.ToString(),vertices=mesh.vertices,normals=mesh.normals,triangles=mesh.triangles});UnityEngine.Object.DestroyImmediate(go);
        }
        float[] tone=new float[24000];for(int i=0;i<tone.Length;i++)tone[i]=.04f*Mathf.Sin(i*440*2*Mathf.PI/24000);
        File.WriteAllBytes(Path.Combine(folder,"audio/tone.wav"),TrialReplayAudio.Encode(tone,1,24000));
        string path=Path.Combine(folder,"recording.jsonl");
        using(var writer=new StreamWriter(path))
        {
            long sequence=0;
            Action<TrialReplayRecord> write=r=>{r.sequence=sequence++;writer.WriteLine(JsonUtility.ToJson(r));};
            write(new TrialReplayRecord{kind="header",recordingId="synthetic-desktop-demo",sourceType="synthetic_demo",participantCode="SYNTHETIC",utc="2026-10-09T00:00:00Z",detail="Invented 3D fixture. No participant measurements. Illustrative tone, not agent speech."});
            for(int trial=0;trial<3;trial++)
            {
                string tid="demo-"+trial;double start=trial*20;
                var objects=new List<TrialReplayObject>();
                for(int wall=0;wall<8;wall++)for(int slot=0;slot<21;slot++)
                {
                    var rotation=Quaternion.Euler(0,wall*45,0);
                    int id=objects.Count;
                    objects.Add(new TrialReplayObject{id="o"+id,mesh=meshes[id%3].id,shape=meshes[id%3].id,color=id%2==0?"Amber":"Blue",rgba=id%2==0?new Color(.92f,.64f,.18f):new Color(.2f,.54f,.85f),target=id==10,
                        position=rotation*new Vector3((slot%7-3)*.3f,.5f+slot/7*.75f+(float)Math.Sin(id*1.3)*.12f,3),rotation=rotation,scale=Vector3.one*.18f,planeId=wall});
                }
                write(new TrialReplayRecord{kind="trial",time=start,trialId=tid,round=trial,participantCode="SYNTHETIC",runNumber=1,sourceTrialId="SYNTHETIC_trial_"+trial,voice="neutral",objects=objects.ToArray(),meshes=trial==0?meshes.ToArray():null,targetId="o10",layout="rotational_scatter_v4",signedTheta=45*(trial+1),absoluteTheta=45*(trial+1),selectionMethod="controller_ray_trigger_press_v1"});
                write(new TrialReplayRecord{kind="search_start",time=start,trialId=tid});
                for(int i=0;i<=960;i++)
                {
                    double t=i/60.0;
                    // Explicitly synthetic Jev fixture for inspection; never a real provider result.
                    if(i==30)write(TrialReviewJevChecks.Fixture("demo-jev-"+trial,tid,false,start+t));
                    if(i==44)write(TrialReviewJevChecks.Fixture("demo-jev-"+trial,tid,true,start+t));
                    if(i==60)write(new TrialReplayRecord{kind="audio_playback_start",time=start+t,trialId=tid,clip="audio/tone.wav",text="Illustrative synthetic tone"});
                    if(i==90)write(new TrialReplayRecord{kind="audio_cancelled",time=start+t,trialId=tid});
                    if(i==300)write(new TrialReplayRecord{kind="search_paused",time=start+t,trialId=tid});
                    if(i==420)write(new TrialReplayRecord{kind="search_resumed",time=start+t,trialId=tid});
                    if(i>=480&&i<510)continue;
                    var head=new Vector3(0,1.55f,0);var rotation=Quaternion.Euler((float)Math.Sin(t*.6)*14, (float)Math.Sin(t*.35)*55,0);
                    var rayOrigin=new Vector3(.22f,1.15f,.1f);var endpoint=objects[10].position;
                    bool active=t<5||t>=7;
                    write(new TrialReplayRecord{kind="sample",time=start+t,trialId=tid,headAvailable=true,headTracked=t>=4&&t<4.3?-1:1,headPosition=head,headRotation=rotation,gazeAvailable=true,gazeTracked=t>=4&&t<4.3?0:1,gazeOrigin=head,gazeDirection=rotation*Vector3.forward,searchActive=active,visible=active,searchSeconds=t<5?t:t<7?5:t-2,
                        rightController=new TrialReplayController{available=true,tracked=1,rayAvailable=true,triggerHeld=i==900?1:0,triggerValue=i==900?1:0,position=rayOrigin,rayOrigin=rayOrigin,rayDirection=(endpoint-rayOrigin).normalized,rayEnd=endpoint,hoveredId=active?"o10":null}});
                    if(i==180)write(new TrialReplayRecord{kind="selection",time=start+t,trialId=tid,objectId="o11",correct=false,searchSeconds=t});
                }
                write(new TrialReplayRecord{kind="selection",time=start+16,trialId=tid,objectId="o10",correct=true,searchSeconds=14});
                write(new TrialReplayRecord{kind="transition",time=start+18,trialId=tid});
            }
            write(new TrialReplayRecord{kind="end",time=60,complete=true});
        }
        return path;
    }
}
