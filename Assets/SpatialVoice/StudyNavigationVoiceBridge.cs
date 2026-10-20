using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SpatialVoice
{
    /// <summary>Lets the catalog/Jev choose coarse turn wording while keeping the study's
    /// normalized, identity-scoped audio and replay telemetry. Streaming is a separate preview
    /// entry point until its loudness and device latency are validated.</summary>
    public sealed class StudyNavigationVoiceBridge : MonoBehaviour
    {
        SpatialVoiceNavigationManager m_Navigation;
        VoiceSynthesizer m_Voice;
        FindObjectGameManager m_Game;
        CancellationTokenSource m_Pending;
        Func<bool> m_Relevant;
        JevDecisionClient m_Decisions;
        CancellationTokenSource m_Warmup;
        bool m_WarmupStarted;
        public bool IsSelecting => m_Pending != null;
        public void Initialize(string gatewayKey, string elevenLabsKey, VoiceSynthesizer voice)
        {
            m_Voice = voice;
            var decisions = m_Decisions = new JevDecisionClient(gatewayKey);
            decisions.Trace += m_Voice.RecordJevTrace;
            m_Voice.TransmissionAdvisor = decisions.Available ? decisions.ShouldTransmitAsync : null;
            m_Voice.InterruptionAdvisor = decisions.Available ? decisions.ShouldInterruptAsync : null;
            m_Game = GetComponent<FindObjectGameManager>();
            m_Navigation = gameObject.AddComponent<SpatialVoiceNavigationManager>();
            m_Navigation.Configure(gatewayKey, elevenLabsKey, () => SessionConfig.Voice == VoiceCondition.SelfSimilar
                ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId, allowStreaming: false);
            m_Navigation.DecisionTrace += m_Voice.RecordJevTrace;
            m_Navigation.Diagnostic += status => m_Voice.RecordNavigationStatus(JsonUtility.ToJson(new CatalogEvent {
                stage = "catalog", status = status, time = Time.realtimeSinceStartupAsDouble }));
        }
        [Serializable] struct CatalogEvent { public string stage, status; public double time; }
        void Update()
        {
            if (!m_WarmupStarted && m_Voice != null && m_Voice.LibraryReady && m_Decisions != null && m_Decisions.Available)
            { m_WarmupStarted = true; WarmConnection(); }
            if (m_Pending != null && (m_Relevant == null || !m_Relevant())) Cancel();
        }
        async void WarmConnection()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                m_Warmup = cancellation;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                string status = "warmup_complete";
                try { await m_Decisions.WarmAsync(cancellation.Token); }
                catch (Exception e) { status = "warmup_" + e.GetType().Name; }
                if (!cancellation.IsCancellationRequested && this != null && isActiveAndEnabled && m_Voice != null)
                    m_Voice.RecordNavigationStatus(JsonUtility.ToJson(new CatalogEvent {
                        stage = "connection_preparation", status = status + ";elapsed_ms=" + timer.ElapsedMilliseconds,
                        time = Time.realtimeSinceStartupAsDouble }));
                if (ReferenceEquals(m_Warmup, cancellation)) m_Warmup = null;
            }
        }
        void OnDisable() { Cancel(); m_Warmup?.Cancel(); }
        public void Cancel() { m_Pending?.Cancel(); }

        public bool TryRequest(Transform target, string route, Func<GuidanceSelection, bool> commit)
        {
            if (IsSelecting || m_Navigation == null || m_Navigation.Database == null || !m_Voice.CanRequestGuidance("catalog_turn") ||
                !TryGeometry(target, out var initial)) return false;
            // Respect the existing route lock near the rear seam. Never reverse a planned
            // turn merely because SignedAngle changes sign at 180 degrees.
            bool left = route == DirectionalHintPolicy.LeftPhrase;
            if (Mathf.Abs(initial.Angle) < 5 || left != (initial.Angle < 0) || Mathf.Abs(initial.Angle) >= 150) return false;
            var candidates = new NavigationPhrase[5];
            if (NavigationGeometry.Filter(m_Navigation.Database.phrases, initial, candidates,
                eligible: p => m_Voice.IsPreparedPhrase(p.text)) == 0) return false;
            StartRequest(target, left, initial, commit);
            return true;
        }

        async void StartRequest(Transform target, bool left, NavigationGeometry initial, Func<GuidanceSelection, bool> commit)
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var condition = SessionConfig.Voice;
                string voiceId = condition == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId;
                int round = m_Game.CurrentObjectiveIndex;
                m_Pending = cancellation;
                m_Relevant = () => this != null && isActiveAndEnabled && m_Voice != null && m_Voice.GuidanceReady &&
                    SessionConfig.Voice == condition && voiceId == (condition == VoiceCondition.SelfSimilar ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId) &&
                    m_Game.CurrentObjectiveIndex == round && TryGeometry(target, out var current) && left == (current.Angle < 0);
                try
                {
                    // Defer one frame so the caller can record its pending state consistently.
                    await Task.Yield();
                    if (!m_Relevant() || !TryGeometry(target, out initial)) return;
                    string runtimeContext = m_Voice.CaptureTransmissionContext();
                    m_Voice.RecordNavigationStatus(runtimeContext);
                    var selection = await m_Navigation.ChooseGuidanceAsync(initial, "Coarse horizontal guidance; target wall not reached.",
                        p => m_Voice.IsPreparedPhrase(p.text), cancellation.Token, runtimeContext);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (selection != null && m_Relevant() && TryGeometry(target, out var current) &&
                        selection.Phrase.Matches(current.Angle, current.Distance) && m_Voice.IsPreparedPhrase(selection.Phrase.text))
                    {
                        bool accepted = commit(selection);
                        if (!accepted) RecordDiscard(selection.RequestId);
                    }
                    else if (selection != null) RecordDiscard(selection.RequestId);
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { Debug.LogWarning("[SpatialVoice] Catalog selection failed: " + e.GetType().Name); }
                finally { m_Pending = null; m_Relevant = null; }
            }
        }
        void RecordDiscard(string id) => m_Voice.RecordJevTrace(new Newtonsoft.Json.Linq.JObject {
            ["schema"] = "jev-trace-v1", ["request_id"] = id, ["stage"] = "application",
            ["outcome"] = "discarded_stale" }.ToString(Newtonsoft.Json.Formatting.None));
        public void MarkSpoken(NavigationPhrase phrase) => m_Navigation.MarkSpoken(phrase);

        public bool IsPhraseCurrent(Transform target, NavigationPhrase phrase) =>
            TryGeometry(target, out var geometry) && phrase.Matches(geometry.Angle, geometry.Distance);

        bool TryGeometry(Transform target, out NavigationGeometry geometry)
        {
            geometry = default;
            if (target == null || !target.gameObject.activeInHierarchy || m_Game == null || !m_Game.SearchActive ||
                m_Game.CurrentState != FindObjectGameManager.GameState.Playing ||
                SharedGazeStream.Instance == null || !SharedGazeStream.Instance.TryGetValid(out var gaze)) return false;
            var info = target.GetComponent<SpawnableObjectInfo>();
            if (info == null || info.shapeName != m_Game.CurrentTarget.shape || info.colorName != m_Game.CurrentTarget.color) return false;
            bool inside = m_Game.IsGazeInSearchSector(gaze.worldOrigin, gaze.worldDirection, info.planeId, out bool valid);
            return valid && !inside && NavigationGeometry.TryMeasure(gaze.worldOrigin, gaze.worldDirection, target.position, out geometry);
        }
        public static string[] Phrases()
        {
            var db = Resources.Load<NavigationPhraseDatabase>("SpatialVoice/NavigationPhrases");
            if (db == null) return Array.Empty<string>();
            var phrases = new string[db.phrases.Length];
            for (int i = 0; i < phrases.Length; i++) phrases[i] = db.phrases[i].text;
            return phrases;
        }
    }
}
