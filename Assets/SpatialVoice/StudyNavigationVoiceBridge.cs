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
        public bool IsSelecting => m_Pending != null;
        public void Initialize(string gatewayKey, string elevenLabsKey, VoiceSynthesizer voice)
        {
            m_Voice = voice;
            var decisions = new JevDecisionClient(gatewayKey);
            m_Voice.InterruptionAdvisor = decisions.Available ? decisions.ShouldInterruptAsync : null;
            m_Game = GetComponent<FindObjectGameManager>();
            m_Navigation = gameObject.AddComponent<SpatialVoiceNavigationManager>();
            m_Navigation.Configure(gatewayKey, elevenLabsKey, () => SessionConfig.Voice == VoiceCondition.SelfSimilar
                ? SessionConfig.SelfSimilarVoiceId : SessionConfig.NeutralVoiceId, allowStreaming: false);
            m_Navigation.Diagnostic += status => m_Voice.RecordNavigationStatus(JsonUtility.ToJson(new CatalogEvent {
                stage = "catalog", status = status, time = Time.realtimeSinceStartupAsDouble }));
        }
        [Serializable] struct CatalogEvent { public string stage, status; public double time; }
        void Update() { if (m_Pending != null && (m_Relevant == null || !m_Relevant())) Cancel(); }
        void OnDisable() => Cancel();
        public void Cancel() { m_Pending?.Cancel(); }

        public bool TryRequest(Transform target, string route, Func<NavigationPhrase, bool> commit)
        {
            if (IsSelecting || m_Navigation == null || m_Navigation.Database == null || !m_Voice.GuidanceReady ||
                !TryGeometry(target, out var initial)) return false;
            // Respect the existing route lock near the rear seam. Never reverse a planned
            // turn merely because SignedAngle changes sign at 180 degrees.
            bool left = route == DirectionalHintPolicy.LeftPhrase;
            if (Mathf.Abs(initial.Angle) < 5 || left != (initial.Angle < 0) || Mathf.Abs(initial.Angle) >= 150) return false;
            var candidates = new NavigationPhrase[4];
            if (NavigationGeometry.Filter(m_Navigation.Database.phrases, initial, candidates,
                eligible: p => m_Voice.IsPreparedPhrase(p.text)) == 0) return false;
            StartRequest(target, left, initial, commit);
            return true;
        }

        async void StartRequest(Transform target, bool left, NavigationGeometry initial, Func<NavigationPhrase, bool> commit)
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
                    var phrase = await m_Navigation.ChooseAsync(initial, "Coarse horizontal guidance; target wall not reached.",
                        p => m_Voice.IsPreparedPhrase(p.text), cancellation.Token);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (phrase.HasValue && m_Relevant() && TryGeometry(target, out var current) &&
                        phrase.Value.Matches(current.Angle, current.Distance) && m_Voice.IsPreparedPhrase(phrase.Value.text))
                    { if (commit(phrase.Value)) m_Navigation.MarkSpoken(phrase.Value); }
                }
                catch (OperationCanceledException) { }
                catch (Exception e) { Debug.LogWarning("[SpatialVoice] Catalog selection failed: " + e.GetType().Name); }
                finally { m_Pending = null; m_Relevant = null; }
            }
        }
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
