using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SpatialVoice;

partial class SpatialVoiceChecks
{
    sealed class RoundCase
    {
        public string id;
        public RoundPerformance facts;
        public Func<string, bool> prepared = _ => true;
        public string[] preferred;
    }

    static RoundCase[] RoundCases() => new[] {
        Case("quick_selection", 5, 0, "quick", "quick_nice", "quick_complete"),
        Case("quick_boundary", 10, 0, "quick", "quick_nice", "quick_complete"),
        Case("above_quick", 10.001, 0, "first_selection", "first_selection_short"),
        Case("first_selection", 12, 0, "first_selection", "first_selection_short"),
        Case("instant_selection", 0, 0, "first_selection", "first_selection_short"),
        Case("just_below_persistence", 29.999, 0, "first_selection", "first_selection_short"),
        Case("persistence_boundary", 30, 0, "persistence", "first_selection"),
        Case("extended_search", 180, 0, "persistence", "first_selection"),
        Case("single_error", 12, 1, "recovery", "recovery_persistence"),
        Case("many_errors", 180, 12, "recovery", "recovery_persistence"),
        new RoundCase { id="many_repeats", facts=new RoundPerformance { measurementsAvailable=true,
            searchSeconds=18, guidanceStarts=30, repeatRequests=12, plannedAbsoluteTurnDegrees=180 },
            preferred=new[] { "first_selection", "first_selection_short" } },
        new RoundCase { id="no_guidance", facts=new RoundPerformance { measurementsAvailable=true, searchSeconds=8,
            plannedAbsoluteTurnDegrees=0 }, preferred=new[] { "quick", "quick_nice", "quick_complete" } },
        new RoundCase { id="practice_with_errors", facts=new RoundPerformance { measurementsAvailable=true,
            practice=true, searchSeconds=90, incorrectSelections=4 }, preferred=new[] { "practice" } },
        new RoundCase { id="missing_measurements", facts=new RoundPerformance { measurementsAvailable=false,
            searchSeconds=90, incorrectSelections=4 }, preferred=GenericIds },
        new RoundCase { id="null_snapshot", facts=null, preferred=GenericIds },
        new RoundCase { id="negative_duration", facts=new RoundPerformance { measurementsAvailable=true,
            searchSeconds=-1 }, preferred=GenericIds },
        new RoundCase { id="negative_error_count", facts=new RoundPerformance { measurementsAvailable=true,
            searchSeconds=12, incorrectSelections=-1 }, preferred=GenericIds },
        new RoundCase { id="only_generic_clips_prepared", facts=new RoundPerformance { measurementsAvailable=true,
            searchSeconds=60, incorrectSelections=1 }, prepared=t=>RoundEncouragement.Phrases.Take(4).Any(p=>p.text==t),
            preferred=GenericIds }
    };

    static readonly string[] GenericIds = { "nice", "well_done", "got_it", "nicely_done" };
    static RoundCase Case(string id, double seconds, int errors, params string[] preferred) => new RoundCase {
        id=id, facts=new RoundPerformance { measurementsAvailable=true, searchSeconds=seconds,
            incorrectSelections=errors, guidanceStarts=3 }, preferred=preferred };

    // Three consecutive simulated rounds also exercise exclusion of the last spoken line.
    // A timeout is reported separately: safe fallback is not a successful model response.
    static async Task LiveEncouragement(string configurationPath)
    {
        var client=new JevDecisionClient((string)JObject.Parse(System.IO.File.ReadAllText(configurationPath))["jev_key"]);
        int responses=0, fallbacks=0, genericInsteadOfSpecific=0;
        foreach(var scenario in RoundCases())
        {
            string previous=null;
            for(int run=0;run<3;run++)
            {
                var candidates=RoundEncouragement.Candidates(scenario.facts,previous,scenario.prepared);
                var clock=System.Diagnostics.Stopwatch.StartNew();
                EncouragementPhrase choice;
                string outcome="response";
                try { choice=await client.ChooseEncouragementAsync(scenario.facts,candidates,CancellationToken.None); responses++; }
                catch(OperationCanceledException) { choice=candidates[0]; fallbacks++; outcome="deadline_fallback"; }
                Check(candidates.Contains(choice) && choice.text!=previous,"Eligible nonrepeating live/fallback choice: "+scenario.id);
                bool specificAvailable=candidates.Any(p=>scenario.preferred.Contains(p.id));
                if(outcome=="response" && specificAvailable && !scenario.preferred.Contains(choice.id)) genericInsteadOfSpecific++;
                previous=choice.text;
                Console.WriteLine("ENCOURAGEMENT case="+scenario.id+";run="+run+";outcome="+outcome+";phrase="+choice.id+";ms="+clock.ElapsedMilliseconds);
            }
        }
        Console.WriteLine("ENCOURAGEMENT SUMMARY responses="+responses+";deadline_fallbacks="+fallbacks+
            ";generic_despite_specific_available="+genericInsteadOfSpecific+";total="+(RoundCases().Length*3));
    }

    static async Task ExtendedEncouragementChecks()
    {
        int combinations=0;
        // Vary availability, practice, time and mistakes independently; test every possible previous line.
        foreach(bool available in new[] { false,true })
        foreach(bool practice in new[] { false,true })
        foreach(double seconds in new[] { -1,0,.1,9.999,10,10.001,29.999,30,180,double.NaN,double.PositiveInfinity,double.NegativeInfinity })
        foreach(int errors in new[] { -1,0,1,12 })
        foreach(string previous in RoundEncouragement.AllTexts().Concat(new string[] { null }))
        {
            var facts=new RoundPerformance { measurementsAvailable=available, practice=practice,
                searchSeconds=seconds, incorrectSelections=errors };
            var choices=RoundEncouragement.Candidates(facts,previous,_=>true);
            Check(choices.Count>=2 && choices.Count<=4 && choices.Select(p=>p.text).Distinct().Count()==choices.Count,
                "Candidate count and uniqueness across edge-case matrix");
            Check(choices.All(p=>p.text!=previous),"Previous wording never repeats");
            bool known=available && seconds>=0 && !double.IsInfinity(seconds) && !double.IsNaN(seconds) && errors>=0;
            foreach(var p in choices)
            {
                if(!GenericIds.Contains(p.id)) Check(known,"Unknown/invalid facts cannot support specific praise");
                if(p.id.StartsWith("first_selection")) Check(known && !practice && errors==0,"First-selection claim supported");
                if(p.id.StartsWith("recovery")) Check(known && !practice && errors>0,"Recovery claim supported");
                if(p.id=="persistence") Check(known && !practice && seconds>=30,"Persistence threshold supported");
                if(p.id.StartsWith("quick")) Check(known && !practice && errors==0 && seconds>0 && seconds<=10,"Quick wording requires positive measured time at or below the threshold with no errors");
                if(p.id=="practice") Check(known && practice,"Practice statement supported");
            }
            combinations++;
        }
        var first=Case("fixture",12,0).facts;
        // Every combination of prepared clips, including none and one: never offer uncached audio.
        for(int mask=0;mask<(1<<RoundEncouragement.Phrases.Length);mask++)
        {
            var prepared=new HashSet<string>(RoundEncouragement.Phrases.Where((p,i)=>(mask&(1<<i))!=0).Select(p=>p.text));
            var choices=RoundEncouragement.Candidates(first,null,prepared.Contains);
            Check(choices.All(p=>prepared.Contains(p.text)),"Only prepared clips are eligible");
        }
        var candidates=RoundEncouragement.Candidates(first,null,_=>true);
        foreach(string invalid in new[] { "{}", "{\"answers\":{}}", "{\"answers\":{\"round_encouragement\":{\"type\":\"text\",\"choice\":\"You selected the right object. Nice work.\"}}}",
            "{\"answers\":{\"round_encouragement\":{\"type\":\"choice\"}}}", Response("You selected the right object. Nice work. Extra words."), Response("nice work."),
            Response("You found it. Nice recovery.") })
            Throws<FormatException>(()=>RoundEncouragement.Parse(invalid,candidates));
        Throws<Newtonsoft.Json.JsonReaderException>(()=>RoundEncouragement.Parse("not json",candidates));
        foreach(int count in new[] {0,1,5})
            Throws<ArgumentException>(()=>RoundEncouragement.Payload(first,RoundEncouragement.Phrases.Take(count).ToList()));

        foreach(var status in new[] { HttpStatusCode.Unauthorized,(HttpStatusCode)429,HttpStatusCode.InternalServerError })
            await ExpectAsync<HttpRequestException>(()=>RequestRound(first,candidates,new RoundResponseHttp("private_provider_body",status)));
        await ExpectAsync<FormatException>(()=>RequestRound(first,candidates,new RoundResponseHttp(new string('x',65537))));
        await ExpectAsync<FormatException>(()=>RequestRound(first,candidates,new RoundResponseHttp(Response("You were faster."))));
        await ExpectAsync<Newtonsoft.Json.JsonReaderException>(()=>RequestRound(first,candidates,new RoundResponseHttp("broken json")));
        using(var stop=new CancellationTokenSource())
        {
            stop.CancelAfter(30);
            await ExpectAsync<OperationCanceledException>(()=>RequestRound(first,candidates,new RoundResponseHttp(null),stop.Token));
        }
        using(var http=new HttpClient(new RoundResponseHttp(Response("You selected the right object. Nice work."))))
            await ExpectAsync<InvalidOperationException>(()=>new JevDecisionClient("",http).ChooseEncouragementAsync(first,candidates,CancellationToken.None));
        Console.WriteLine("PASS encouragement: "+combinations+" fact/history combinations; "+(1<<RoundEncouragement.Phrases.Length)+" prepared-clip subsets; malformed/out-of-set replies, HTTP failures, response limit and external cancellation");
    }

    static string Response(string text) => new JObject { ["answers"]=new JObject { ["round_encouragement"]=new JObject {
        ["type"]="choice", ["choice"]=text } } }.ToString();
    static async Task<EncouragementPhrase> RequestRound(RoundPerformance facts,List<EncouragementPhrase> choices,
        HttpMessageHandler handler,CancellationToken token=default)
    {
        using(var http=new HttpClient(handler)) return await new JevDecisionClient("synthetic",http).ChooseEncouragementAsync(facts,choices,token);
    }
    static async Task ExpectAsync<T>(Func<Task> action) where T:Exception
    {
        try { await action(); }
        catch(T error) { Check(!error.Message.Contains("private_provider_body"),"Provider body is not exposed in errors"); return; }
        throw new Exception("Expected "+typeof(T).Name);
    }
    sealed class RoundResponseHttp:HttpMessageHandler
    {
        readonly string body; readonly HttpStatusCode status;
        public RoundResponseHttp(string body,HttpStatusCode status=HttpStatusCode.OK) { this.body=body;this.status=status; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(body==null) await Task.Delay(10000,token);
            return new HttpResponseMessage(status) { Content=new StringContent(body??"") };
        }
    }
}
