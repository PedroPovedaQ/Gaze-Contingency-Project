using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

partial class VoiceIsolationChecks
{
    static IEnumerator VerifiedFixture(string key,byte[] audio,string expected,Action<VoiceAudioVerification.Result> done)
    { yield return null; done(new VoiceAudioVerification.Result { matches=true,transcript=expected }); }
    static void CheckParallelDownloads(string root)
    {
        Check(VoiceAudioVerification.Matches("You're on it!", "You are on it."),"Harmless punctuation/contraction difference accepted");
        Check(VoiceAudioVerification.Matches("All right, look left.","Alright look left"),"Spelling variant accepted without dropping direction");
        Check(VoiceAudioVerification.Matches("Turn right 165 degrees.","Turn right one hundred and sixty-five degrees."),"Spoken numbers match digits without weakening angle checks");
        Check(!VoiceAudioVerification.Matches("Turn right 165 degrees.","Turn right fifteen degrees."),"Incorrect numeric instruction is rejected");
        Check(VoiceAudioVerification.Matches("Keep looking up and right.", "Keep looking up and write"),
            "Observed directional right/write ASR homophone must not block preparation");
        Check(VoiceAudioVerification.Matches("Go back up and right.", "Go back up and write"),
            "Observed go-back directional homophone matches");
        Check(!VoiceAudioVerification.Matches("Look right.", "Look left."), "Direction changes remain rejected");
        Check(!VoiceAudioVerification.Matches("More, more.", "More."), "Missing repeated word remains rejected");
        Check(VoiceAudioVerification.Matches("Turn left 47.5 degrees.", "Turn left forty seven point five degrees."),
            "Decimal angle retains spoken point");
        Check(!VoiceAudioVerification.Matches("Turn left 47.5 degrees.", "Turn left forty seven, five degrees."),
            "Missing decimal point must not earn a receipt");
        Check(!VoiceAudioVerification.Matches("Turn left 47.5 degrees.", "Turn left 475 degrees."),
            "Incorrect decimal magnitude remains rejected");
        Check((string)Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json("Turn left 47.5 degrees.", "model"))["text"] ==
            "Turn left forty seven point five degrees.", "Decimal angles are explicit from the first synthesis attempt");
        foreach(var heard in new[] { "Um look left", "Look right", "Look left please", "Look", "Look left ugh", "Look left (sneezes)", "" })
            Check(!VoiceAudioVerification.Matches("Look left.",heard),"Added/missing/changed words or events rejected: "+heard);
        var initial = Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json("Turn right 165 degrees.", "model", 0));
        var repair = Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json("Turn right 165 degrees.", "model", 1));
        var finalRepair = Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json("Okay, look left.", "model", 2));
        Check((string)initial["text"] == "Turn right 165 degrees." && initial["voice_settings"] == null,
            "Initial generation preserves existing request settings");
        Check((string)repair["text"] == "Turn right one hundred sixty five degrees." && (string)repair["model_id"] == "model" &&
            (float)repair["voice_settings"]["stability"] == .85f, "First repair spells numbers and stabilizes the same model");
        Check((float)finalRepair["voice_settings"]["stability"] == 1 && !(bool)finalRepair["voice_settings"]["use_speaker_boost"] &&
            (string)finalRepair["text"] == "Okay, look left.", "Final repair disables boost without changing approved wording");
        Check(VoiceSynthesisRequest.ExpandIntegers("1.5 meters; 165 degrees") == "1.5 meters; one hundred sixty five degrees",
            "Integer expansion never changes decimal distances");
        foreach (string direction in new[] { "left", "right" })
        {
            string phrase = "Look slightly " + direction + ".";
            var repaired = Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json(phrase, "model", 2));
            Check((string)repaired["text"] == "Look, slightly " + direction + ".",
                "Final repair separates look/slightly without adding or changing words");
            Check(VoiceAudioVerification.Matches(phrase, (string)repaired["text"]),
                "Repair punctuation preserves the exact canonical script");
            Check(!VoiceAudioVerification.Matches(phrase, "Looks slightly " + direction),
                "Repair does not weaken the verbatim gate");
            Check((string)Newtonsoft.Json.Linq.JObject.Parse(VoiceSynthesisRequest.Json(phrase, "model", 1))["text"] == phrase,
                "Earlier attempts preserve original phrasing");
        }
        VoiceAudioVerification.Result verified=null;
        UnityWebRequest.VerificationResponse="{\"text\":\"Look left.\"}";
        Drain(VoiceAudioVerification.Verify("synthetic",new byte[120],"Look left.",r=>verified=r));
        Check(verified.matches && verified.error==null,"Production verifier accepts matching transcript");
        Check(UnityWebRequest.LastForm.Fields["no_verbatim"]=="false" && UnityWebRequest.LastForm.Fields["tag_audio_events"]=="true" &&
            !UnityWebRequest.LastForm.Fields.ContainsKey("keyterms"),"ASR retains fillers/events and gets no expected-text hint");
        UnityWebRequest.VerificationResponse="{\"text\":\"Look left um.\"}";
        Drain(VoiceAudioVerification.Verify("synthetic",new byte[120],"Look left.",r=>verified=r));
        Check(!verified.matches && verified.error==null,"Mismatch is distinguished from a transport failure");
        UnityWebRequest.StatusCodes.Enqueue(403);
        Drain(VoiceAudioVerification.Verify("synthetic",new byte[120],"Look left.",r=>verified=r));
        Check(!verified.matches && verified.error.Contains("403"),"Verification access failure is closed");
        UnityWebRequest.VerificationResponse="bad JSON";
        Drain(VoiceAudioVerification.Verify("synthetic",new byte[120],"Look left.",r=>verified=r));
        Check(!verified.matches && verified.error!=null,"Malformed verification response rejected");
        foreach(var filler in new[] { "Um, look left.", "Uhhh, turn right.", "Look up, ugh.", "Hmm." })
            Check(VoiceAudioIntegrity.HasForbiddenFiller(filler),"Filler word blocked before synthesis");
        Check(!VoiceAudioIntegrity.HasForbiddenFiller("Turn toward the column."),"Filler filter respects whole words");
        Check(VoiceAudioIntegrity.IsRejectedHash("fc0e8723298e34f4e275d200db18ac36276f763a62bf1d55fbc8fc4980e6f450"),"Known bad neutral clip is rejected");
        Check(!VoiceAudioIntegrity.IsRejectedAudio(new byte[120]),"Quarantine does not reject unrelated clips");
        int active=0, peak=0, finished=0;
        var operations=new List<AsyncOperation>();
        IEnumerator Work()
        {
            active++; peak=Math.Max(peak,active);
            try
            {
                var operation=new AsyncOperation { isDone=false }; operations.Add(operation);
                yield return operation;
                finished++;
            }
            finally { active--; }
        }
        var jobs=new List<IEnumerator>();for(int i=0;i<13;i++) jobs.Add(Work());
        var batch=new VoiceDownloadBatch(jobs);var runner=batch.Run();
        Check(runner.MoveNext() && active==5,"Five requests start before waiting on any of them");
        for(int i=0;i<8;i++) Check(runner.MoveNext() && active==5 && finished==0,"Pending requests do not oversubscribe five slots");
        // Complete out of order, then make one slot available for another job.
        operations[3].isDone=true;runner.MoveNext();runner.MoveNext();
        Check(finished==1 && active==5 && operations.Count==6,"A completed request frees one slot immediately");
        int frames=0;
        while(runner.MoveNext())
        {
            foreach(var operation in operations) operation.isDone=true;
            Check(++frames<100,"Download batch makes progress");
        }
        Check(peak==5 && finished==13 && active==0 && batch.Completed==13,"All requests complete within bounded concurrency");

        active=0;finished=0;operations.Clear();jobs.Clear();
        for(int i=0;i<9;i++) jobs.Add(Work());
        batch=new VoiceDownloadBatch(jobs);runner=batch.Run();runner.MoveNext();
        batch.Dispose();batch.Dispose();
        foreach(var operation in operations) operation.isDone=true;
        Check(!runner.MoveNext() && active==0 && finished==0,"Cancellation disposes active jobs and never starts queued jobs");

        IEnumerator Broken() { yield return null; throw new IOException("synthetic"); }
        batch=new VoiceDownloadBatch(new[] { Work(),Broken(),Work() });runner=batch.Run();
        while(runner.MoveNext()) { }
        Check(batch.Error is IOException && active==0,"Worker exception releases sibling requests");

        // Drive the actual synthesizer download jobs with fake HTTP, keeping audio/DSP tests separate.
        string cache=Path.Combine(root,"parallel-downloads");Directory.CreateDirectory(cache);
        var synth=new VoiceSynthesizer();Set(synth,"m_CacheDir",cache);Set(synth,"m_ApiKey","synthetic");
        Set(synth,"m_VerifyAudio",new Func<string,byte[],string,Action<VoiceAudioVerification.Result>,IEnumerator>(VerifiedFixture));
        Set(synth,"m_CloneClient",new ElevenLabsVoiceClient { Audio=new byte[120] });
        SessionConfig.SelfSimilarVoiceId="download-clone";SessionConfig.SelfSimilarEnrollmentPending=false;
        SessionConfig.Voice=VoiceCondition.SelfSimilar;
        object MakeJobs(string phrase)
        {
            var lines=new List<KeyValuePair<VoiceCondition,string>> {
                new KeyValuePair<VoiceCondition,string>(VoiceCondition.Generic,phrase),
                new KeyValuePair<VoiceCondition,string>(VoiceCondition.Generic,phrase),
                new KeyValuePair<VoiceCondition,string>(VoiceCondition.SelfSimilar,phrase) };
            return typeof(VoiceSynthesizer).GetMethod("DownloadJobs",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(synth,new object[] {lines});
        }
        IEnumerator Download(object downloadJobs) => (IEnumerator)typeof(VoiceSynthesizer)
            .GetMethod("DownloadLines",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(synth,new object[] {downloadJobs,null});
        var captured=MakeJobs("Parallel identity test.");
        Drain(Download(captured));
        Check(((IList)captured).Count==2 && Directory.GetFiles(cache,"*.mp3").Length==2,
            "Duplicate lines deduplicate; neutral and clone caches remain distinct");
        Check(SessionConfig.Voice==VoiceCondition.SelfSimilar,"Parallel download never mutates the selected voice");
        string auditedFile=Directory.GetFiles(cache,"*.mp3")[0];
        var receipt=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(auditedFile+".verified.json"));
        string expected=(string)receipt["expected"];
        Check(VoiceAudioVerification.HasReceipt(auditedFile,expected),"Verified cache has a receipt for these exact bytes and script");
        Check(!VoiceAudioVerification.HasReceipt(auditedFile,"Different script."),"Receipt cannot be reused for another script");
        receipt["version"]="obsolete";File.WriteAllText(auditedFile+".verified.json",receipt.ToString());
        Check(!VoiceAudioVerification.HasReceipt(auditedFile,expected),"Old verification policy cannot admit audio");
        receipt["version"]=VoiceAudioVerification.Version;receipt["transcript"]="Um wrong words";
        File.WriteAllText(auditedFile+".verified.json",receipt.ToString());
        Check(!VoiceAudioVerification.HasReceipt(auditedFile,expected),"Mismatched receipt transcript cannot admit audio");
        receipt["transcript"]=expected;File.WriteAllText(auditedFile+".verified.json",receipt.ToString());
        var changed=new byte[120];changed[0]=1;File.WriteAllBytes(auditedFile,changed);
        Check(!VoiceAudioVerification.HasReceipt(auditedFile,expected),"Tampering audio invalidates its receipt");
        File.WriteAllBytes(auditedFile,new byte[120]);
        UnityWebRequest.Requests.Clear();Drain(Download(captured));
        Check(UnityWebRequest.Requests.Count==0,"Cached downloads make no HTTP request");
        Check(Directory.GetFiles(cache,"*.download").Length==0,"Atomic writes leave no temporary downloads");

        UnityWebRequest.StatusCodes.Enqueue(429);UnityWebRequest.StatusCodes.Enqueue(503);UnityWebRequest.StatusCodes.Enqueue(200);
        UnityWebRequest.Requests.Clear();Drain(Download(MakeJobs("Retry transient test.")));
        Check(UnityWebRequest.Requests.Count==3,"429 and 503 retry within the three-attempt limit");
        UnityWebRequest.StatusCodes.Enqueue(401);UnityWebRequest.Requests.Clear();
        Drain(Download(MakeJobs("Authentication test.")));
        Check(UnityWebRequest.Requests.Count==1,"Authentication rejection is not retried");
        UnityWebRequest.StatusCodes.Enqueue(429);UnityWebRequest.StatusCodes.Enqueue(429);UnityWebRequest.StatusCodes.Enqueue(429);
        UnityWebRequest.Requests.Clear();Drain(Download(MakeJobs("Quota exhausted test.")));
        Check(UnityWebRequest.Requests.Count==3,"Persistent quota failure has bounded retries");
        int before=Directory.GetFiles(cache,"*.mp3").Length;
        UnityWebRequest.ContentType="application/json";Drain(Download(MakeJobs("Wrong content type.")));
        Check(Directory.GetFiles(cache,"*.mp3").Length==before+1,"Non-audio neutral response is never cached; independent valid clone still completes");
        UnityWebRequest.ContentType="audio/mpeg";

        UnityWebRequest.HoldRequests=true;
        captured=MakeJobs("Cancelled request.");var downloading=Download(captured);
        Check(downloading.MoveNext(),"Request is pending before cancellation");
        synth.Stop();Drain(downloading);
        Check(UnityWebRequest.Aborts>0 && Directory.GetFiles(cache,"*.download").Length==0,
            "Stopping the synthesizer aborts pending HTTP and cleans partial downloads");
        UnityWebRequest.HoldRequests=false;
        bool callback=false;
        Drain(synth.PrepareLibraries(new[] { "Reset during preparation." },
            stage=> { if(stage.StartsWith("Downloading")) synth.ResetVoicePreparation(); },
            ok=>callback=true,neutralOnly:true));
        Check(!callback && !synth.LibraryReady,"Cancelled preparation cannot call back ready or overwrite a new setup");
        // A decode already yielded to Unity when reset occurs must not refill the new library.
        var file=Directory.GetFiles(cache,"*.mp3")[0];
        var decodeReceipt=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file+".verified.json"));
        Set(synth,"m_PreparingText",(string)decodeReceipt["expected"]);
        var decode=(IEnumerator)typeof(VoiceSynthesizer).GetMethod("PlayFromFile",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(synth,new object[] { file,false,SessionConfig.NeutralVoiceId,true });
        Check(decode.MoveNext(),"Decode waits for its file request");
        synth.ResetVoicePreparation();
        if(decode.Current is IEnumerator child) Drain(child);
        Drain(decode);
        var admitted=(IDictionary)typeof(VoiceSynthesizer).GetField("m_PreparedClips",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(synth);
        Check(admitted.Count==0,"Late decoded audio cannot enter a reset library");
        int checks=0;
        IEnumerator RejectTwice(string key,byte[] bytes,string text,Action<VoiceAudioVerification.Result> done)
        { yield return null; checks++;done(new VoiceAudioVerification.Result {matches=checks>=3,transcript=checks>=3?text:"Um wrong words"}); }
        Set(synth,"m_VerifyAudio",new Func<string,byte[],string,Action<VoiceAudioVerification.Result>,IEnumerator>(RejectTwice));
        Drain(Download(MakeJobs("Bounded repair succeeds.")));
        Check(checks>=3,"A mismatched generation is regenerated and rechecked");
        IEnumerator AlwaysReject(string key,byte[] bytes,string text,Action<VoiceAudioVerification.Result> done)
        { yield return null; checks++;done(new VoiceAudioVerification.Result {matches=false,transcript="Um wrong words"}); }
        checks=0;before=Directory.GetFiles(cache,"*.mp3").Length;
        Set(synth,"m_VerifyAudio",new Func<string,byte[],string,Action<VoiceAudioVerification.Result>,IEnumerator>(AlwaysReject));
        UnityWebRequest.RequestBodies.Clear();
        var rejectedJobs=MakeJobs("Bounded repair exhausted.");
        Drain(Download(rejectedJobs));
        Check(UnityWebRequest.RequestBodies.Count==3 &&
            Newtonsoft.Json.Linq.JObject.Parse(UnityWebRequest.RequestBodies[0])["voice_settings"]==null &&
            (float)Newtonsoft.Json.Linq.JObject.Parse(UnityWebRequest.RequestBodies[1])["voice_settings"]["stability"]==.85f &&
            !(bool)Newtonsoft.Json.Linq.JObject.Parse(UnityWebRequest.RequestBodies[2])["voice_settings"]["use_speaker_boost"],
            "Actual download retries progressively change synthesis settings instead of repeating the failed request");
        var rejectedJob=((IList)rejectedJobs)[0];
        Check(((string)rejectedJob.GetType().GetField("error").GetValue(rejectedJob)).Contains("Bounded repair exhausted."),
            "Exhausted verification identifies the affected script in the setup error");
        Check(checks==6 && Directory.GetFiles(cache,"*.mp3").Length==before,"Three failed generations per voice never publish playable audio");
        IEnumerator Unavailable(string key,byte[] bytes,string text,Action<VoiceAudioVerification.Result> done)
        { yield return null; checks++;done(new VoiceAudioVerification.Result {error="Verification unavailable"}); }
        Set(synth,"m_VerifyAudio",new Func<string,byte[],string,Action<VoiceAudioVerification.Result>,IEnumerator>(Unavailable));
        checks=0;Drain(Download(MakeJobs("Verifier unavailable.")));
        Check(checks==2 && Directory.GetFiles(cache,"*.mp3").Length==before,"Verifier outage never caches unchecked speech or wastes synthesis retries");
        File.Delete(auditedFile+".verified.json");
        checks=0;Drain(Download(MakeJobs(expected)));
        Check(checks>=1 && !VoiceAudioVerification.HasReceipt(auditedFile,expected),
            "Legacy cache cannot bypass unavailable verification");
        Set(synth,"m_VerifyAudio",new Func<string,byte[],string,Action<VoiceAudioVerification.Result>,IEnumerator>(VerifiedFixture));
        Drain(Download(MakeJobs(expected)));
        Check(VoiceAudioVerification.HasReceipt(auditedFile,expected),"Legacy cache earns a receipt when verification recovers");
        Console.WriteLine("PASS: five concurrent downloads, out-of-order completion, cancellation, failures, deduplication, voice-scoped caching, bounded retries and content validation.");
    }
}
