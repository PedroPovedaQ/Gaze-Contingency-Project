using System;
using System.Collections.Generic;

namespace SpatialVoice
{
    /// <summary>Development action menu. Unity supplies verified geometry/history and prepared audio;
    /// Jev may choose a helpful action, but cannot invent a direction or qualify a target.</summary>
    public static class NavigationActionMenu
    {
        public const string Policy = "jev_actions_overrides_v1";
        public static bool CanUse(bool enabled, string action, bool valid, bool looking,
            bool controllerOnTarget, bool stable, bool ready) => enabled && valid && !looking &&
            !controllerOnTarget && stable && ready && (action == "Direction" || action == "ReturnTarget" ||
                action == "ReturnBearing" || action == "ReturnWall");

        public static List<NavigationPhrase> Build(string action, string primary, string explicitDirection,
            string encouragement, bool inside, bool progress, Predicate<string> prepared)
        {
            var choices = new List<NavigationPhrase>();
            bool correction = action == "ReturnTarget" || action == "ReturnBearing" || action == "ReturnWall";
            void Add(string id, string text)
            {
                if (string.IsNullOrEmpty(text) || !prepared(text)) return;
                foreach (var choice in choices) if (choice.text == text) return;
                choices.Add(new NavigationPhrase { id = id, text = text,
                    tone = id == "encourage" ? NavigationTone.Encouraging : NavigationTone.Direct,
                    metadata = SpeechPhraseMetadata.Describe(text) });
            }
            if (correction) Add("correct", primary);
            Add(inside ? "refine" : "orient", explicitDirection);
            if (!correction && progress) Add("encourage", encouragement);
            return choices;
        }
    }
    public interface INavigationActionDecisionProvider
    {
        System.Threading.Tasks.Task<GuidanceSelection> ChooseActionAsync(string state,
            IReadOnlyList<NavigationPhrase> choices, System.Threading.CancellationToken cancellation);
    }
}
