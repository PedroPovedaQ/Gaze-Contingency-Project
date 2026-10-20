using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class TrialReviewEvaluationChecks
{
    static void Assert(bool value,string message)
    {if(!value)throw new Exception("Evaluation: "+message);}
    public static void Run()
    {
        var folder=Path.Combine(Path.GetTempPath(),"trial-evaluation-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path=Path.Combine(folder,"evaluation.json");
            File.WriteAllText(path,"{\"schema\":\"guidance-evaluation-v1\",\"provenance\":{\"recording_sha256\":\"recording-a\"}}");
            Assert(TrialReviewEvaluation.ReadReport(path,"recording-a")!=null,"exact recording accepted");
            bool rejected=false;
            try{TrialReviewEvaluation.ReadReport(path,"recording-b");}
            catch(InvalidDataException){rejected=true;}
            Assert(rejected,"different recording rejected");
            string pilot=Path.Combine(folder,"pilots.json");
            File.WriteAllText(pilot,"{\"schema\":\"guidance-paired-pilots-v1\",\"measurement_basis\":\"simulated clock, not headset\",\"variants\":[]}");
            Assert(TrialReviewEvaluation.ReadPilots(pilot)!=null,"explicit simulated comparison accepted separately");
            File.WriteAllText(pilot,"{\"schema\":\"guidance-paired-pilots-v1\",\"variants\":[]}");
            rejected=false;try{TrialReviewEvaluation.ReadPilots(pilot);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"comparison must declare its measurement basis");
            var report=JObject.Parse("{\"batches\":[{\"status\":\"success\",\"case_map\":{\"case-1\":\"source-case\"},\"result\":{\"cases\":[{\"case_id\":\"case-1\",\"ratings\":{\"direction\":{\"verdict\":\"unknown\",\"rationale\":\"No geometry\",\"evidence_ids\":[]}}}]}}]}");
            Assert((string)TrialReviewEvaluation.Rating(report,"source-case")["direction"]["verdict"]=="unknown","unknown remains unknown");
            Assert(TrialReviewEvaluation.Rating(report,"other")==null,"unmatched case never receives a rating");
            report["batches"][0]["status"]="failed";
            Assert(TrialReviewEvaluation.Rating(report,"source-case")==null,"failed batch never presents cached-looking success");
            Assert(TrialReviewEvaluation.Rating(null,"source-case")==null,"missing judge is not a passing score");
        }
        finally{Directory.Delete(folder,true);}
        Debug.Log("TRIAL_REVIEW_EVALUATION_PASS recording identity, exact joins, unknown and failure states");
    }
}
