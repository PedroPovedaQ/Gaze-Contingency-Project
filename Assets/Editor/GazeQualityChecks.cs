using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GazeQualityChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Codex/Check Gaze Measurement")]
    public static void Run()
    {
        var go=new GameObject("CoordinateFixture");
        try
        {
            go.transform.position=new Vector3(1,2,3);go.transform.rotation=Quaternion.Euler(0,90,0);
            var local=new Vector3(.03f,0,0);
            var raw=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,false);
            var flipped=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,true);
            Check(Vector3.Distance(raw.direction,Vector3.right)<.0001f,"Head/tracking-space rotation applies once");
            Check(Vector3.Distance(raw.origin,go.transform.TransformPoint(local))<.0001f,"Origin transforms into world space");
            Check(Mathf.Abs(Vector3.Distance(raw.origin,flipped.origin)-.06f)<.0001f,"Diagnostic X mirror changes origin only");
            Check(local.x==.03f,"Coordinate conversion does not mutate raw input");
            var again=GazeCoordinates.InputPoseToWorld(local,Quaternion.identity,go.transform,true);
            Check(again.origin==flipped.origin,"No cumulative flip between reads");
            Check(VoiceAssistantController.RoundPhrase(1,"Red","Cube")==VoiceAssistantController.RoundPhrase(1,"Blue","Sphere"),"Round speech cannot reveal target identity");
            CheckAgentIntroduction();
            foreach(var phrase in DirectionalHintPolicy.AllPhrases())
                Check(VoicePromptText.FormatForVoice(phrase,VoiceCondition.Generic)==VoicePromptText.FormatForVoice(phrase,VoiceCondition.SelfSimilar),"Shared voice wording");
            CheckNativeReadinessFallback();
            CheckCsvIntegration();
            CheckTrialLinkage();
            CheckStopAfterCapture();
            CheckDecisionSnapshots();
            CheckOpeningLifecycleAndHistory();
            CheckSameWallOpening();
            CheckPointingVicinity();
            CheckValidationPanelClearance();
            Debug.Log("[GazeQualityChecks] PASS: coordinates, idempotent diagnostic flip, hidden target, shared wording, shared-stream CSV validity and append safety. Hardware accuracy/rate NOT validated.");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    static void CheckAgentIntroduction()
    {
        var neutral = typeof(SessionConfig).GetProperty("NeutralQuickTest");
        var clone = typeof(SessionConfig).GetProperty("CloneQuickTest");
        bool wasNeutral = SessionConfig.NeutralQuickTest, wasClone = SessionConfig.CloneQuickTest;
        try
        {
            neutral.SetValue(null,false); clone.SetValue(null,false);
            string practice = VoiceAssistantController.RoundPhrase(0,"Red","Cube",true);
            Check(practice.StartsWith(VoiceAssistantController.AgentIntroduction),"Practice introduces the guide");
            Check(!VoiceAssistantController.RoundPhrase(0,"Red","Cube").StartsWith(VoiceAssistantController.AgentIntroduction),"Measured study block does not repeat practice introduction");
            Check(practice == VoiceAssistantController.RoundPhrase(0,"Blue","Sphere",true),"Introduction does not disclose target identity");
            foreach (var voice in new[] { VoiceCondition.Generic, VoiceCondition.SelfSimilar })
                Check(VoicePromptText.FormatForVoice(practice,voice)==practice,"Introduction wording is shared between voices");
            neutral.SetValue(null,true);
            string first = VoiceAssistantController.RoundPhrase(0,"Red","Cube");
            Check(first.StartsWith(VoiceAssistantController.AgentIntroduction),"Quick test introduces the guide despite skipping study onboarding");
            Check(Array.IndexOf(VoiceAssistantController.PhraseLibrary(),first)>=0,"Quick-test introduction is prepared for playback");
            Check(!VoiceAssistantController.RoundPhrase(1,"Red","Cube").StartsWith(VoiceAssistantController.AgentIntroduction),"Later trials do not repeat the greeting");
        }
        finally { neutral.SetValue(null,wasNeutral); clone.SetValue(null,wasClone); }
    }
    static void CheckSameWallOpening()
    {
        if (SharedGazeStream.Instance != null) throw new InvalidOperationException("Stop Play mode before gaze checks.");
        var host = new GameObject("OpeningFixture"); host.SetActive(false);
        var source = new GameObject("OpeningEyes");
        var target = new GameObject("HiddenOpeningTarget"); // No renderer required before reveal.
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var hints = host.AddComponent<HintGenerator>();
            typeof(HintGenerator).GetField("m_GameManager",flags).SetValue(hints,game);
            typeof(FindObjectGameManager).GetField("m_CurrentTarget",flags).SetValue(game,("Cube","Red",Color.red));
            var info = target.AddComponent<SpawnableObjectInfo>();
            info.shapeName="Cube"; info.colorName="Red"; info.planeId=0;
            ((System.Collections.Generic.List<GameObject>)typeof(FindObjectGameManager)
                .GetField("m_SpawnedObjects",flags).GetValue(game)).Add(target);
            var stream = source.AddComponent<SharedGazeStream>();
            typeof(SharedGazeStream).GetProperty("Instance").SetValue(null,stream);
            var sample = new GazeObservation { valid=true,worldOrigin=new Vector3(0,1.5f,0),worldDirection=Vector3.forward };
            void Refresh() {
                sample.inputEventTime=sample.receiptTime=Time.realtimeSinceStartupAsDouble;
                typeof(SharedGazeStream).GetProperty("Latest").SetValue(stream,sample);
            }
            foreach (float x in new[] { -.4f,0,.4f })
                foreach (float y in new[] { -1f,1f })
                {
                    target.transform.position=new Vector3(x,1.5f+y,1.5f); Refresh();
                    string expected="Look " + (y>0 ? "higher" : "lower") + (x==0 ? "" : x<0 ? " and left" : " and right") + ".";
                    Check(hints.TryPrepareOpening(out var phrase,out var id) && phrase==expected && !string.IsNullOrEmpty(id),
                        "Hidden same-wall target gets vertical/diagonal opening with a recorded decision");
                    Check(typeof(HintGenerator).GetField("m_OpeningTurn",flags).GetValue(hints)==null,
                        "Within-wall opening does not seed an inter-wall turn");
                }
            sample.worldDirection=Vector3.right; Refresh();
            Check(hints.TryPrepareOpening(out var outside,out _) && outside=="The object is to the left.",
                "Other-wall opening retains the initial left/right cue");
            sample.valid=false; Refresh();
            Check(!hints.TryPrepareOpening(out _,out _),"Invalid gaze cannot invent a pre-trial direction");
            sample.valid=true; Refresh(); sample.inputEventTime-=1;
            Check(!hints.TryPrepareOpening(out _,out _),"Stale gaze cannot invent a pre-trial direction");
            Debug.Log("[GazeQualityChecks] PASS: hidden-target same-wall opening, diagonals, other-wall cue and invalid/stale gaze.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(target); typeof(SharedGazeStream).GetProperty("Instance").SetValue(null,null);
        }
    }
    static void CheckPointingVicinity()
    {
        var host = new GameObject("PointingVicinityFixture"); host.SetActive(false);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var other = new GameObject("Distractor");
        var dev = typeof(SessionConfig).GetProperty("NeutralQuickTest");
        bool originalDev = SessionConfig.NeutralQuickTest;
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            target.transform.position = new Vector3(0,0,2); target.transform.localScale = Vector3.one * .1f;
            var info = target.AddComponent<SpawnableObjectInfo>(); info.objectId = "target-1";
            Physics.SyncTransforms();
            var bounds = target.GetComponent<Collider>().bounds;
            var nearby = Quaternion.Euler(0,6,0) * Vector3.forward;
            Check(GazeTargetVicinity.Measure(Vector3.zero, nearby, bounds, 6, out float error, out float radius) &&
                error < radius && error > 5 && radius > 8 && radius < 9, "Circular margin catches nearby gaze outside the object");
            Check(GazeTargetVicinity.Measure(Vector3.zero, Quaternion.Euler(0,12,0)*Vector3.forward,
                bounds,6,out error,out radius) && error > radius, "Gaze outside the circle is rejected");
            Check(!GazeTargetVicinity.Measure(Vector3.zero,new Vector3(float.NaN,0,1),bounds,6,out _,out _),"Invalid ray has no vicinity");
            Check(!GazeTargetVicinity.Measure(bounds.center,Vector3.forward,bounds,6,out _,out _),"Eye inside bounds has no useful angular region");
            var vicinity = host.AddComponent<GazeTargetVicinity>();
            dev.SetValue(null,false); vicinity.SetPadding(15); vicinity.SetRingVisible(true);
            Check(vicinity.PaddingDegrees==6 && !vicinity.RingVisible,"Study sessions cannot enable ring or adjust pilot radius");
            dev.SetValue(null,true); vicinity.SetPadding(12); vicinity.SetRingVisible(true);
            Check(vicinity.PaddingDegrees==12 && vicinity.RingVisible,"Development admin can change radius and ring");
            GazeTargetVicinity.Measure(Vector3.zero,nearby,bounds,vicinity.PaddingDegrees,out error,out radius);
            host.SetActive(true); vicinity.Draw(Vector3.zero,bounds,radius,true);
            var ring=host.GetComponentInChildren<LineRenderer>();
            Check(ring!=null && ring.enabled && ring.GetComponent<Collider>()==null,"Development ring renders without changing selection collision");
            for(int i=0;i<ring.positionCount;i++)
                Check(Mathf.Abs(Vector3.Angle(ring.GetPosition(i),bounds.center)-radius)<.01f,"Rendered ring matches the angular acceptance radius");
            CaptureVicinityRing(target,ring);
            dev.SetValue(null,false); vicinity.Draw(Vector3.zero,bounds,radius,true);
            Check(!ring.enabled,"Switching to study mode hides an already-created ring");
            host.SetActive(false);dev.SetValue(null,true);
            vicinity.SetPadding(100); Check(vicinity.PaddingDegrees==20,"Radius plus clamps at 20 degrees");
            vicinity.SetPadding(-1); Check(vicinity.PaddingDegrees==0,"Radius minus cannot shrink below the object");
            vicinity.SetPadding(6); dev.SetValue(null,false);
            Check(!vicinity.RingVisible && vicinity.PaddingDegrees==6,"Leaving development cannot leak the target ring or radius into study");
            var controller = host.AddComponent<ControllerRaySelector>();
            var game = host.AddComponent<FindObjectGameManager>();
            var objects = (System.Collections.Generic.List<GameObject>)typeof(FindObjectGameManager)
                .GetField("m_SpawnedObjects",flags).GetValue(game);
            objects.Add(target); objects.Add(other);
            var hints = host.AddComponent<HintGenerator>();
            void Set(string name,object value) => typeof(HintGenerator).GetField(name,flags).SetValue(hints,value);
            Set("m_GameManager",game); Set("m_Controller",controller); Set("m_Vicinity",vicinity); Set("m_EvaluatedTarget",info);
            Set("m_EvaluatedSample",new GazeObservation { valid=true,worldOrigin=Vector3.zero,worldDirection=nearby });
            var sample = new ControllerRaySelector.TelemetrySample { available=true,rayAvailable=true,tracked=1,target=target };
            void Telemetry(ControllerRaySelector.TelemetrySample value,double age=0,int frames=0)
            {
                typeof(ControllerRaySelector).GetProperty("RightTelemetry").SetValue(controller,value);
                typeof(ControllerRaySelector).GetProperty("TelemetryFrame").SetValue(controller,Time.frameCount-frames);
                typeof(ControllerRaySelector).GetProperty("TelemetryTime").SetValue(controller,Time.realtimeSinceStartupAsDouble-age);
            }
            bool Pointing(bool valid=true)
            {
                // This fixture injects a new observation/geometry directly, bypassing
                // Evaluate, which normally clears the per-observation vicinity cache.
                Set("m_VicinityMeasured", false);
                return (bool)typeof(HintGenerator).GetMethod("EvaluatePointing",flags).Invoke(hints,new object[]{valid});
            }
            Telemetry(sample); Check(Pointing(),"Right ray on actual target plus nearby gaze qualifies");
            Check(!Pointing(false),"Invalid gaze never qualifies even with exact controller hit");
            Check((bool)typeof(HintGenerator).GetField("m_ControllerOnTarget",flags).GetValue(hints),
                "Blink keeps controller evidence available without allowing a gaze-free announcement");
            bool Looking(bool valid=true) {
                Pointing(valid);
                return (bool)typeof(HintGenerator).GetMethod("EvaluateLooking",flags).Invoke(hints,new object[]{valid});
            }
            bool ExactHit() => (bool)typeof(HintGenerator).GetField("m_GazeOnTarget",flags).GetValue(hints);
            target.layer=8; Physics.SyncTransforms();
            Check(Looking() && !ExactHit(),"Expanded ring qualifies while exact intersection remains separately false");
            Telemetry(default);
            Check(Looking(),"Gaze confirmation does not require controller tracking or a controller hit");
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=Vector3.right});
            Check(!Looking(),"Outside the ring does not qualify");
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=Vector3.forward});
            Check(Looking() && !Looking(false),"Direct target gaze qualifies only with valid eye data");
            var occluder=other.AddComponent<BoxCollider>(); other.layer=8;
            other.transform.position=Vector3.forward; other.transform.localScale=Vector3.one*.1f; Physics.SyncTransforms();
            Check(!Looking(),"Overlapping collinear rings are ambiguous and must not confirm the hidden target");
            other.AddComponent<SpawnableObjectInfo>().objectId="distractor-1";
            other.transform.position=Quaternion.Euler(0,6,0)*Vector3.forward*2; Physics.SyncTransforms();
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=nearby});
            Telemetry(sample);
            Check(!Looking() && !Pointing(),"Distractor closer to gaze wins overlapping rings even with controller on target");
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=Vector3.forward});
            Check(Looking(),"Target wins when its angular center is closer in overlapping rings");
            var third=GameObject.CreatePrimitive(PrimitiveType.Cube); third.transform.localScale=Vector3.one*.1f;
            third.AddComponent<SpawnableObjectInfo>().objectId="distractor-2";
            objects.Add(third);
            try {
                third.transform.position=Quaternion.Euler(0,-4,0)*Vector3.forward*2; Physics.SyncTransforms();
                Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,
                    worldDirection=Quaternion.Euler(0,-3,0)*Vector3.forward});
                Check(!Looking(),"Closest distractor wins among three overlapping rings");
                objects.Reverse(); Check(!Looking(),"Winner cannot depend on spawn-list order"); objects.Reverse();
                third.SetActive(false); Check(Looking(),"Inactive objects do not compete");
                third.SetActive(true); third.GetComponent<Collider>().enabled=false;
                Check(Looking(),"Disabled colliders do not compete");
            } finally { objects.Remove(third); UnityEngine.Object.DestroyImmediate(third); }
            occluder.enabled=false; Telemetry(sample);
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=nearby});
            var machine=(GuidanceStateMachine)typeof(HintGenerator).GetField("m_State",flags).GetValue(hints);
            var hold=(GazeZoneEntryGate)typeof(GuidanceStateMachine).GetField("m_Pointing",flags).GetValue(machine);
            hold.Update(true,true,0);
            Check(!hold.Update(true,true,.99) && hold.Update(true,true,1.0),"Runtime pointing gate requires 1 second");
            sample.target=other;Telemetry(sample);Check(!Pointing(),"Pointing at a distractor never confirms the target");
            sample.target=target;sample.tracked=0;Telemetry(sample);Check(!Pointing(),"Untracked controller cannot confirm");
            sample.tracked=1;Telemetry(sample,.2);Check(!Pointing(),"Old controller telemetry cannot confirm after a stall");
            Telemetry(sample,0,2);Check(!Pointing(),"Skipped controller frames cannot confirm");
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=Vector3.back});
            Telemetry(sample);Check(!Pointing(),"Controller alone cannot confirm while gaze is elsewhere");
            Check(DirectionalHintPolicy.WithinArea(10,10)==DirectionalHintPolicy.UpRightPhrase &&
                DirectionalHintPolicy.WithinArea(-10,-10)==DirectionalHintPolicy.DownLeftPhrase,"Arrival and follow-up combine vertical and horizontal corrections within the wall");
            var voice=host.AddComponent<VoiceSynthesizer>(); Set("m_Voice",voice);
            var gazeHold=(GazeZoneEntryGate)typeof(GuidanceStateMachine).GetField("m_Looking",flags).GetValue(machine);
            var wallHold=(GazeZoneEntryGate)typeof(GuidanceStateMachine).GetField("m_Wall",flags).GetValue(machine);
            void Evidence(bool valid,bool inside,bool looking) => typeof(HintGenerator).GetMethod("UpdateSpeechEvidence",flags)
                .Invoke(hints,new object[]{valid,inside,looking});
            sample.target=target;sample.tracked=1;sample.triggerHeld=1;Telemetry(sample);
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=Vector3.forward,
                inputEventTime=Time.realtimeSinceStartupAsDouble,receiptTime=Time.realtimeSinceStartupAsDouble});
            hold.Reset();gazeHold.Reset();wallHold.Reset();
            hold.Update(true,true,0);gazeHold.Update(true,true,0);wallHold.Update(true,true,0);
            Evidence(true,true,Looking());
            var e=voice.GuidanceEvidence;
            Check(e.gazeRayHit=="target" && e.controllerRayHit=="target" && e.gazeWithinTargetVicinity &&
                !e.targetGazeQualified && !e.controllerTargetHoldQualified && !e.wallEntryQualified && e.controllerTriggerHeld==1,
                "Jev snapshot distinguishes immediate hits from uncompleted holds");
            hold.Update(true,true,1);gazeHold.Update(true,true,1);wallHold.Update(true,true,1);
            Evidence(true,true,Looking());
            Check(e.targetGazeQualified && e.controllerTargetHoldQualified && e.wallEntryQualified,
                "Jev qualified flags reflect completed gates from the current tick");
            sample.target=other;Telemetry(sample);occluder.enabled=true;Physics.SyncTransforms();
            Set("m_EvaluatedSample",new GazeObservation {valid=true,worldOrigin=Vector3.zero,worldDirection=nearby});
            Evidence(true,true,Looking());
            Check(e.gazeRayHit=="distractor" && e.controllerRayHit=="distractor" && !e.targetGazeQualified && !e.controllerOnTarget,
                "Jev knows when either ray intersects a distractor");
            sample.tracked=0;Telemetry(sample);Evidence(false,false,Looking(false));
            Check(e.gazeRayHit=="unavailable" && e.controllerRayHit=="unavailable" && e.controllerTriggerHeld==-1 && !e.targetGazeQualified,
                "Tracking loss records unknown rays and trigger rather than false evidence");
            hints.CancelPending();
            Check(!voice.GuidanceEvidence.valid && voice.GuidanceEvidence.phase==GuidancePhase.Inactive.ToString(),
                "Cancelled trial cannot leak its old guidance evidence");
            Debug.Log("[GazeQualityChecks] PASS: pointing vicinity, target identity, freshness, vertical arrival and development-only radius/ring controls");
        }
        finally
        {
            dev.SetValue(null,originalDev);
            var vicinity=host.GetComponent<GazeTargetVicinity>();
            if(vicinity!=null)
            {
                var materialField=typeof(GazeTargetVicinity).GetField("m_Material",flags);
                var material=(Material)materialField.GetValue(vicinity);
                materialField.SetValue(vicinity,null);
                if(material!=null)UnityEngine.Object.DestroyImmediate(material);
            }
            UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(other);
        }
    }
    static void CaptureVicinityRing(GameObject target, LineRenderer ring)
    {
        // Camera.Render can crash native URP line rendering with the Null device.
        // Keep structural checks headless; run this visual capture with a GPU.
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.Log("[GazeQualityChecks] Vicinity capture skipped: Null graphics device; GPU verification required.");
            return;
        }
        int targetLayer=target.layer, ringLayer=ring.gameObject.layer;
        var cameraObject=new GameObject("VicinityPreviewCamera",typeof(Camera));
        var output=new RenderTexture(800,600,24);
        var image=new Texture2D(800,600,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            target.layer=ring.gameObject.layer=31;
            var camera=cameraObject.GetComponent<Camera>();camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.05f,.07f);
            camera.fieldOfView=40;camera.nearClipPlane=.01f;camera.farClipPlane=10;
            camera.targetTexture=output;camera.Render();RenderTexture.active=output;
            image.ReadPixels(new Rect(0,0,800,600),0,0);image.Apply();
            File.WriteAllBytes("/tmp/gaze-vicinity-ring-preview.png",image.EncodeToPNG());
        }
        finally
        {
            target.layer=targetLayer;ring.gameObject.layer=ringLayer;RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(cameraObject);output.Release();
            UnityEngine.Object.DestroyImmediate(output);UnityEngine.Object.DestroyImmediate(image);
        }
    }
    static void CheckNativeReadinessFallback()
    {
        // Reproduce DEV_NEUTRAL run 002: fresh EXT pose reports untracked while HTC eyes are valid.
        GazeObservation Sample() => new GazeObservation {
            tracked=false, poseValid=true, focused=true, headValid=true, valid=false,
            invalidReason="untracked", receiptTime=10, inputEventTime=10, htcQueryReceiptTime=10.001,
            htcGazeAvailable=true, leftValid=true, rightValid=true,
            leftWorldOrigin=new Vector3(-.03f,1,0), rightWorldOrigin=new Vector3(.03f,1,0),
            leftWorldDirection=Vector3.forward, rightWorldDirection=Vector3.forward,
            rawWorldDirection=Vector3.right };
        var s=Sample(); SharedGazeStream.ResolveUsableRay(s,true,10.01,.1);
        Check(s.valid && s.worldDirection==Vector3.forward, "Valid native eyes must charge readiness when EXT isTracked is false");
        Check(s.raySource=="HTC_binocular_mean" && !s.tracked && s.rawWorldDirection==Vector3.right, "Fallback preserves original EXT flags and raw ray");
        foreach(string failure in new[]{"blink","stale_native","stale_input","focus","head","parent","missing_native","bad_ray"})
        {
            s=Sample();
            if(failure=="blink")s.leftValid=s.rightValid=false;
            if(failure=="stale_native")s.htcQueryReceiptTime=9;
            if(failure=="stale_input")s.inputEventTime=9;
            if(failure=="focus")s.focused=false;
            if(failure=="head")s.headValid=false;
            if(failure=="missing_native")s.htcGazeAvailable=false;
            if(failure=="bad_ray")s.leftWorldDirection=new Vector3(float.NaN,0,0);
            SharedGazeStream.ResolveUsableRay(s,failure!="parent",10.01,.1);
            Check(!s.valid,"Native fallback must reject "+failure);
        }
        s=Sample();s.leftValid=false;SharedGazeStream.ResolveUsableRay(s,true,10.01,.1);
        Check(!s.valid,"Binocular fallback does not silently become monocular");
        s=Sample();s.valid=true;s.tracked=true;s.worldDirection=Vector3.up;
        SharedGazeStream.ResolveUsableRay(s,true,10.01,.1);
        Check(s.worldDirection==Vector3.up,"A valid EXT ray remains preferred");
        s.leftValid=s.rightValid=false;SharedGazeStream.ResolveUsableRay(s,true,10.01,.1);
        Check(!s.valid,"Native tracking loss still vetoes EXT");
        Debug.Log("[GazeQualityChecks] PASS: native-eye readiness fallback, raw preservation and invalid/stale rejection");
    }

    static void CheckOpeningLifecycleAndHistory()
    {
        var host = new GameObject("HintOpeningLifecycle"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var hint = host.AddComponent<HintGenerator>();
            typeof(HintGenerator).GetField("m_GameManager", flags).SetValue(hint, game);
            hint.OnNewObjective();
            Check(!(bool)typeof(HintGenerator).GetField("m_TipsSuppressed",flags).GetValue(hint),"Search enables ongoing guidance");
            Check((float)typeof(HintGenerator).GetField("m_LastTipTime",flags).GetValue(hint)==Time.time,
                "Post-opening search waits the regular cadence instead of repeating the opening immediately");
            var remember=typeof(HintGenerator).GetMethod("RememberDirection",flags);
            var continuation=typeof(HintGenerator).GetField("m_ContinuationUsed",flags);
            remember.Invoke(hint,new object[]{DirectionalHintPolicy.RightPhrase}); continuation.SetValue(hint,true);
            remember.Invoke(hint,new object[]{DirectionalHintPolicy.RightPhrase});
            Check((bool)continuation.GetValue(hint),"Same-direction progress cannot rearm generic encouragement");
            remember.Invoke(hint,new object[]{DirectionalHintPolicy.DownPhrase});
            Check(!(bool)continuation.GetValue(hint),"A new direction permits one fresh continuation");
            continuation.SetValue(hint,true); hint.OnNewObjective();
            Check(!(bool)continuation.GetValue(hint),"New trial resets continuation history");
            typeof(HintGenerator).GetField("m_OpeningTurn",flags).SetValue(hint,DirectionalHintPolicy.LeftPhrase);
            hint.OnNewObjective();
            var state=(GuidanceStateMachine)typeof(HintGenerator).GetField("m_State",flags).GetValue(hint);
            var turn=state.Turn;
            Check(turn.Resolve(179,false)==DirectionalHintPolicy.LeftPhrase,"Runtime preserves opening route across rear seam");
            turn.MarkSpoken(DirectionalHintPolicy.LeftPhrase,180);
            Check(turn.ProgressDue(DirectionalHintPolicy.LeftPhrase,135),"Runtime turn state recognizes a new milestone");
            turn.MarkSpoken(DirectionalHintPolicy.LeftPhrase,135);
            Check(!turn.ProgressDue(DirectionalHintPolicy.LeftPhrase,135),"Runtime does not replay consumed milestone");
            var revisit=(GazeReturnCueGate)typeof(GuidanceStateMachine).GetField("m_Revisit",flags).GetValue(state);
            revisit.Update(true,true,0); revisit.Update(true,true,.11); revisit.Update(true,false,.2);
            Check(revisit.Update(true,false,.46),"Runtime retains a qualified recent target departure");
            hint.CancelPending();
            Check(!revisit.IsPending(.5) && turn.Resolve(179,false)==DirectionalHintPolicy.RightPhrase,"Cancellation clears target memory and opening route");
            hint.OnNewObjective();
            var lookingGate=(GazeZoneEntryGate)typeof(GuidanceStateMachine).GetField("m_Looking",flags).GetValue(state);
            lookingGate.Update(true,true,0);
            Check(!lookingGate.Update(true,true,.099) && lookingGate.Update(true,true,.1),"Runtime gaze gate uses exactly one tenth of a second");
            lookingGate.MarkSpoken(.1); lookingGate.Update(true,false,1); lookingGate.Update(true,false,1.16);
            lookingGate.Update(true,true,1.2);
            Check(lookingGate.Update(true,true,1.71),"Runtime confirmation rearms after a brief real look-away without a cooldown");
            hint.CancelPending();
            Check((bool)typeof(HintGenerator).GetField("m_TipsSuppressed",flags).GetValue(hint),"Pause cancels ongoing guidance");
            hint.OnNewObjective();
            Check(!(bool)typeof(HintGenerator).GetField("m_TipsSuppressed",flags).GetValue(hint),"Resume restores ongoing guidance without an opening state");
            var decision = new GuidanceDecision { targetGeometryAvailable = true, targetYawError = -4,
                targetHistory = new[] { new GazeTargetHistory.Sample { time=1.2, inputTime=1.19,
                    eyeRecordingId="fixture-eyes", observationSequence=17, yawError=.5f, pitchError=1 } } };
            var restored = JsonUtility.FromJson<GuidanceDecision>(JsonUtility.ToJson(decision));
            Check(restored.targetGeometryAvailable && restored.targetHistory.Length == 1 &&
                restored.targetHistory[0].eyeRecordingId == "fixture-eyes" && restored.targetHistory[0].observationSequence == 17,
                "Guidance decision serializes exact history links without changing source identity");
            Debug.Log("[GazeQualityChecks] PASS: first-cue trial/resume lifecycle and target-history serialization");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Serializable] class StopSummary { public string session_outcome; public StopTrial[] objectives; }
    [Serializable] class StopTrial { public bool completed; public string outcome; public float time_to_find_seconds; public float search_exposure_seconds; }

    static void CheckStopAfterCapture()
    {
        // Invoke the real stop handler and read its actual summary/event files.
        // A success-audio failure is a session stop, not a reversal of selection.
        string directory = Path.Combine(Application.temporaryCachePath, "TrialStopChecks_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string originalFolder = SessionConfig.CurrentRunFolder;
        var folder = typeof(SessionConfig).GetProperty("CurrentRunFolder");
        var host = new GameObject("TrialStopFixture"); host.SetActive(false);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var loggerType = typeof(TrialDataLogger);
        var recordType = loggerType.GetNestedType("ObjectiveRecord", BindingFlags.NonPublic);
        try
        {
            folder.SetValue(null, directory);
            var game = host.AddComponent<FindObjectGameManager>();
            var logger = host.AddComponent<TrialDataLogger>();
            loggerType.GetField("m_GameManager", flags).SetValue(logger, game);
            loggerType.GetField("m_SessionId", flags).SetValue(logger, "STOP_FIXTURE");
            var records = (System.Collections.IList)loggerType.GetField("m_ObjectiveRecords", flags).GetValue(logger);
            foreach (int round in new[] { 0, ChallengeSet.RoundsPerBlock - 1, ChallengeSet.TotalRounds - 1 })
            foreach (bool captured in new[] { true, false })
            {
                records.Clear();
                for (int index = 0; index < ChallengeSet.TotalRounds; index++)
                {
                    object record = Activator.CreateInstance(recordType);
                    void Set(string name, object value) => recordType.GetField(name).SetValue(record, value);
                    Set("index", index); Set("startTime", index == round ? 1f : -1f);
                    Set("completed", index == round && captured);
                    Set("outcome", index == round ? (captured ? "completed" : "searching") : "not_started");
                    Set("searchSeconds", index == round && captured ? 8f : 0f);
                    Set("voiceCondition", SessionConfig.VoiceLabelForRound(index));
                    records.Add(record);
                }
                loggerType.GetField("m_ActiveObjectiveIndex", flags).SetValue(logger, round);
                typeof(FindObjectGameManager).GetField("m_CurrentRound", flags).SetValue(game, round + (captured ? 1 : 0));
                loggerType.GetField("m_EventWriter", flags).SetValue(logger, new StreamWriter(Path.Combine(directory,"events.csv")));
                loggerType.GetMethod("OnSessionStopped", flags).Invoke(logger, new object[] { "audio_failure:success" });
                var saved = JsonUtility.FromJson<StopSummary>(File.ReadAllText(Path.Combine(directory,"trial_summary.json")));
                Check(saved.session_outcome == "technical_failure", "Stop keeps the session's technical failure");
                Check(saved.objectives[round].completed == captured &&
                    saved.objectives[round].outcome == (captured ? "completed" : "technical_failure"),
                    "A stop cannot undo completed selection, including block and final boundaries");
                Check(!captured || (saved.objectives[round].time_to_find_seconds == 8f && saved.objectives[round].search_exposure_seconds == 8f),
                    "Success interruption preserves the captured search duration");
                Check(File.ReadAllText(Path.Combine(directory,"events.csv")).Contains("session_stopped"), "Stop event is retained and flushed");
                Check(loggerType.GetField("m_EventWriter", flags).GetValue(logger) == null, "Stop closes the event writer");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            folder.SetValue(null, originalFolder);
            Directory.Delete(directory, true);
        }
    }

    static void CheckValidationPanelClearance()
    {
        var type=typeof(GazeDiagnosticView);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var offset=(Vector3)type.GetMethod("PanelOffset",flags).Invoke(null,new object[]{true});
        var size=(Vector2)type.GetField("k_PanelSize",flags).GetValue(null);
        float scale=(float)type.GetField("k_CanvasScale",flags).GetRawConstantValue();
        Vector2 half=size*scale*.5f;
        // Project known local validation directions onto the actual canvas plane.
        // Cover fit/drift +/-15 degrees and held-out +/-9/+3/+15 elevations.
        foreach(float yaw in new[]{-15f,0,15f,22.5f,29.5f})
        foreach(float pitch in new[]{-15f,-12f,-9f,0,3f,12f,15f})
        {
            Vector3 ray=Quaternion.Euler(-pitch,yaw,0)*Vector3.forward;
            Vector3 hit=ray*(offset.z/ray.z)-offset;
            Check(Mathf.Abs(hit.x)>half.x+.02f || Mathf.Abs(hit.y)>half.y+.02f,
                $"Diagnostic panel must not hide the independent target at yaw {yaw}, pitch {pitch}");
        }
    }
    static void CheckDecisionSnapshots()
    {
        var sample = new GazeObservation { sequence=17, recordingId="eye-recording", participant="p1",
            run=2, round=39, block=0, sourceTrialId="p1_run002_r39", inputEventTime=10,
            receiptTime=10.01, valid=true, deviceAvailable=true, source="fixture", phase="search" };
        var request=GuidanceDecision.Capture("request-a","requested","stable_direction","Look up.",
            "A little higher.",sample,10.02,true,true,false,true);
        sample.sequence=18;sample.valid=false;sample.invalidReason="stale";
        var rejected=GuidanceDecision.Capture("request-b","suspended","stale",null,"",sample,
            10.2,false,false,false,false);
        Check(request.observationSequence==17 && request.gazeUsable && request.eyeRecordingId=="eye-recording" &&
            request.sourceTrialId=="p1_run002_r39", "Decision freezes the original gaze and trial identity");
        Check(Math.Abs(request.gazeAgeAtDecision-.02)<.0001 && request.uncertaintyStatus=="not_estimated_pilot_thresholds",
            "Decision records delivery age without inventing measured uncertainty");
        Check(!rejected.gazeUsable && rejected.invalidReason=="stale" && rejected.observationSequence==18,
            "Suspension preserves the invalid observation and reason");
        var saved=JsonUtility.FromJson<GuidanceDecision>(JsonUtility.ToJson(request));
        Check(saved.decisionId=="request-a" && saved.phrase=="A little higher." && saved.observationSequence==17,
            "Persisted decision retains speech and source linkage");
    }
    static void CheckTrialLinkage()
    {
        var host = new GameObject("GazeTrialLinkageFixture"); host.SetActive(false);
        var originalVoice = SessionConfig.Voice;
        try
        {
            var game = host.AddComponent<FindObjectGameManager>();
            var current = typeof(FindObjectGameManager).GetField("m_CurrentRound", BindingFlags.Instance | BindingFlags.NonPublic);
            var present = typeof(FindObjectGameManager).GetMethod("SetPresentedTrialContext", BindingFlags.Instance | BindingFlags.NonPublic);
            var sample = new GazeObservation { participant="TEST", run=3 };
            SharedGazeStream.FillTrialContext(sample, game);
            Check(sample.round == -1 && sample.block == -1 && sample.sourceTrialId == "", "Setup cannot invent a trial");
            foreach (int round in new[] { 0, ChallengeSet.RoundsPerBlock - 1, ChallengeSet.TotalRounds - 1 })
            {
                current.SetValue(game,round); present.Invoke(game,null);
                string voice = game.PresentedVoiceTag;
                current.SetValue(game,round+1); // Real capture advances before success finishes.
                SessionConfig.Voice = SessionConfig.Voice == VoiceCondition.Generic ? VoiceCondition.SelfSimilar : VoiceCondition.Generic;
                SharedGazeStream.FillTrialContext(sample,game);
                Check(sample.round==round && sample.sourceTrialId==$"TEST_run003_r{round:D2}" &&
                    sample.block==round/ChallengeSet.RoundsPerBlock && sample.voice==voice,"Feedback preserves completed eye trial and voice, including block/final boundary");
            }
            typeof(FindObjectGameManager).GetProperty("IsPractice").SetValue(game,true);
            current.SetValue(game,0);present.Invoke(game,null);current.SetValue(game,ChallengeSet.RoundsPerBlock);
            SharedGazeStream.FillTrialContext(sample,game);
            Check(sample.practice && sample.round==0 && sample.block==-1 && sample.sourceTrialId=="",
                "Practice feedback stays practice and cannot join a measured trial");
            present.Invoke(game,null);SharedGazeStream.FillTrialContext(sample,game);
            Check(sample.round==ChallengeSet.RoundsPerBlock && sample.practice,"Only the next presentation advances the recording context");
        }
        finally { SessionConfig.Voice=originalVoice; UnityEngine.Object.DestroyImmediate(host); }
    }
    static void CheckCsvIntegration()
    {
        if (SharedGazeStream.Instance != null) throw new InvalidOperationException("Stop Play mode before gaze checks.");
        string directory = Path.Combine(Application.temporaryCachePath, "GazeCsvChecks_" + Guid.NewGuid().ToString("N"));
        string originalFolder = SessionConfig.CurrentRunFolder, originalParticipant = SessionConfig.ParticipantId;
        var folderProperty = typeof(SessionConfig).GetProperty("CurrentRunFolder");
        var latestProperty = typeof(SharedGazeStream).GetProperty("Latest");
        var host = new GameObject("GazeCsvFixture"); host.SetActive(false);
        var source = new GameObject("SharedGazeFixture");
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            folderProperty.SetValue(null, directory); SessionConfig.ParticipantId = "CSV_FIXTURE";
            var stream = source.AddComponent<SharedGazeStream>();
            // Edit-mode components do not run normal player lifecycle methods.
            typeof(SharedGazeStream).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stream,null);
            var logger = host.AddComponent<GazeDataLogger>();
            void Call(string method) => typeof(GazeDataLogger).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(logger, null);
            Call("OpenLog");
            target.layer = 8; target.transform.position = new Vector3(1000, 1000, 1002);
            var info = target.AddComponent<SpawnableObjectInfo>();
            info.objectId = "fixture-object"; info.shapeName = "Cube"; info.colorName = "Blue";
            Physics.SyncTransforms();
            var sample = new GazeObservation { sequence = 1, deviceAvailable = true, valid = true,
                inputEventTime = Time.realtimeSinceStartupAsDouble, receiptTime = Time.realtimeSinceStartupAsDouble,
                worldOrigin = new Vector3(1000,1000,1000), worldDirection = Vector3.forward,
                participant = "SNAPSHOT", run = 3, round = 39, voice = "selfsimilar", practice = true,
                sourceTrialId = "fixture_trial", maxDeliveryAgeSeconds = .1f };
            latestProperty.SetValue(stream, sample);
            Call("Update"); Call("OnDisable");
            Call("OpenLog"); // This used to silently truncate a run's CSV.
            sample.sequence = 2; sample.valid = false; sample.invalidReason = "untracked";
            latestProperty.SetValue(stream, sample);
            Call("Update"); Call("OnDisable");
            var lines = File.ReadAllLines(Path.Combine(directory,"gaze_log.csv"));
            Check(lines.Length == 3, "Re-enable must preserve the first row and a single header.");
            string[] header = lines[0].Split(','), valid = lines[1].Split(','), invalid = lines[2].Split(',');
            Check(header.Length == valid.Length && header.Length == invalid.Length, "Every row matches the CSV schema.");
            string Cell(string[] row, string field) => row[Array.IndexOf(header, field)];
            Check(Cell(valid,"gaze_valid") == "1" && Cell(valid,"hovered_object_id") == "fixture-object", "CSV uses the shared valid ray intersection.");
            Check(Cell(valid,"participant_id") == "SNAPSHOT" && Cell(valid,"run_number") == "3" &&
                Cell(valid,"voice_condition") == "selfsimilar" && Cell(valid,"is_practice") == "1",
                "Compatibility CSV preserves observation identity rather than mutable global state");
            Check(Cell(valid,"objective_index") == "39", "CSV objective index uses the presented trial context");
            Check(Cell(invalid,"gaze_valid") == "0" && Cell(invalid,"gaze_origin_x") == "" && Cell(invalid,"hovered_object_id") == "", "Tracking loss retains a gap without a stale pose or object.");
            Check(Cell(valid,"blink") == "" && Cell(valid,"blink_count") == "" && Cell(valid,"fixation_x") == "", "Unvalidated eye events remain unavailable.");
            Check(logger.BlinkCount == -1 && !logger.HasBlinkSignal, "Legacy callers cannot consume an unvalidated blink count.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(target);
            typeof(SharedGazeStream).GetProperty("Instance").SetValue(null,null);
            folderProperty.SetValue(null, originalFolder); SessionConfig.ParticipantId = originalParticipant;
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }

}
