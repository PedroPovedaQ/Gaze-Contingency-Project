using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class StudyIntakeChecks
{
    static string Hash(string path) { using (var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
    static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    [MenuItem("Tools/Codex/Check Study Intake")]
    public static void Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"study-intake-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"PreparationLogs")); Directory.CreateDirectory(Path.Combine(root,"tts_cache"));
        var a=new ParticipantSessionAssignment {schema=2,id=Guid.NewGuid().ToString("N"),participantCode="P999999",configuration="A",
            neutralGender="Male",cloneVoiceId="synthetic_clone",consentId=Guid.NewGuid().ToString("N"),
            intakeProtocol="audio-intake-v2-20261028",eligibilityReviewed=true,development=true,cacheManifestSha256=new string('a',64)};
        string template=Path.Combine(root,"PreparationLogs/study-audio-template.json");
        File.WriteAllText(template,new JObject { ["keyPrefix"]="fixture|", ["phrases"]=new JArray(new JObject {["request"]="{\"text\":\"Synthetic check\"}"}) }.ToString());
        var entries=new JArray();string audio="";
        foreach(string scope in new[] {"el-"+SessionConfig.NeutralId(NeutralVoiceProfile.Male,0),"el-clone-"+a.cloneVoiceId})
        {
            string name;
            using(var sha=SHA256.Create()) name="tts_"+BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes("fixture|"+scope+"|Synthetic check"))).Replace("-","").ToLowerInvariant()+".mp3";
            audio=Path.Combine(root,"tts_cache",name);File.WriteAllText(audio,"synthetic fixture");File.WriteAllText(audio+NeutralDecodedAudio.Suffix,"synthetic fixture");
            entries.Add(new JObject {["file"]=name});
        }
        var manifest=new JObject { ["id"]=a.id,["participantCode"]=a.participantCode,["consentId"]=a.consentId,["cloneVoiceId"]=a.cloneVoiceId,
            ["neutralGender"]=a.neutralGender,["neutralOption"]=a.neutralOption,["development"]=true,["templateSha256"]=Hash(template),
            ["entries"]=entries };
        string receipt=Path.Combine(root,"PreparationLogs","intake-audio-"+a.id+".json");File.WriteAllText(receipt,manifest.ToString());a.cacheManifestSha256=Hash(receipt);
        var validate=typeof(VoiceModeSelector).GetMethod("ValidateIntakeAudioAt",BindingFlags.Static|BindingFlags.NonPublic);
        Action run=()=>validate.Invoke(null,new object[]{a,root});
        Action reject=()=>{bool rejected=false;try {run();} catch(TargetInvocationException) {rejected=true;} Check(rejected,"Invalid intake receipt accepted");};
        try
        {
            a.Validate();run();Check(!a.NeutralFirst,"Configuration A must be self-similar first");
            a.configuration="B";Check(a.NeutralFirst,"Configuration B must be selected first");
            a.participantCode="P888888";reject();a.participantCode="P999999";
            a.cacheManifestSha256=new string('b',64);reject();a.cacheManifestSha256=Hash(receipt);
            var removed=entries[1];entries.RemoveAt(1);File.WriteAllText(receipt,manifest.ToString());a.cacheManifestSha256=Hash(receipt);reject();
            entries.Add(removed);File.WriteAllText(receipt,manifest.ToString());a.cacheManifestSha256=Hash(receipt);run();
            File.Delete(audio+NeutralDecodedAudio.Suffix);reject();File.WriteAllText(audio+NeutralDecodedAudio.Suffix,"fixture");
            File.WriteAllText(template,"changed runtime template");reject();
            a.schema=1;reject();
            a.schema=2;a.eligibilityReviewed=false;bool denied=false;try {a.Validate();} catch(ArgumentException) {denied=true;}Check(denied,"Eligibility must be recorded");
            var priorIntake=SessionConfig.DesktopIntake;bool priorLocal=SessionConfig.LocalAdaptiveModel;
            var intakeProperty=typeof(SessionConfig).GetProperty("DesktopIntake",BindingFlags.Public|BindingFlags.Static);
            try
            {
                a.development=false;intakeProperty.SetValue(null,a);SessionConfig.TrySetLocalAdaptiveModel(true);
                Check(!SessionConfig.TrySetLocalAdaptiveModel(false) && SessionConfig.LocalAdaptiveModel,"Study must retain Local MiniLM");
                a.development=true;Check(SessionConfig.TrySetLocalAdaptiveModel(false),"Development provider comparison must remain available");
            }
            finally {intakeProperty.SetValue(null,priorIntake);SessionConfig.TrySetLocalAdaptiveModel(priorLocal);}
            Check(SessionConfig.NeutralId(NeutralVoiceProfile.Male,0)!="SAz9YHcvj6GT2YYXdXww","Intake voice leaked into catalog");
            var survey=typeof(TrialDataLogger).GetMethod("BuildFinalSurveyUrl",BindingFlags.Static|BindingFlags.NonPublic);
            string url=(string)survey.Invoke(null,new object[]{"P999999","session","run",false,"Voice & name","handoff"});
            Check(url.Contains("condition_order=A&selected_voice_block=2&self_similar_voice_block=1") && url.Contains("selected_voice_name=Voice%20%26%20name"),"Survey mapping/encoding failed");
            url=(string)survey.Invoke(null,new object[]{"P999999","session","run",true,"Voice","handoff"});
            Check(url.Contains("condition_order=B&selected_voice_block=1&self_similar_voice_block=2"),"Survey condition B mapping failed");
            Debug.Log("[StudyIntakeChecks] PASS: consent/eligibility contract, both condition orders, participant isolation, manifest/template integrity, incomplete transfer and legacy intake rejection. Synthetic development data only.");
        }
        finally { Directory.Delete(root,true); }
    }
}
