using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SpatialVoice
{
    /// <summary>Conversational advice only. Geometry, timing, cancellation and
    /// final playback eligibility remain local, regardless of provider.</summary>
    public interface INavigationDecisionProvider
    {
        bool Available { get; }
        string Name { get; }
        event Action<string> Trace;
        Task<NavigationPhrase> ChooseAsync(string state, IReadOnlyList<NavigationPhrase> candidates,
            CancellationToken cancellation, int timeoutMilliseconds = GuidanceSpeechPolicy.CatalogChoiceDeadlineMilliseconds);
        Task<GuidanceSelection> ChooseGuidanceAsync(string state, IReadOnlyList<NavigationPhrase> candidates,
            CancellationToken cancellation);
        Task<bool> ShouldTransmitAsync(string state, CancellationToken cancellation);
        Task<bool> ShouldInterruptAsync(string state, CancellationToken cancellation);
        Task WarmAsync(CancellationToken cancellation);
    }

    /// <summary>Enforces the offered set for every provider, including injected test providers.</summary>
    public static class NavigationDecisionRequests
    {
        public static async Task<GuidanceSelection> Choose(INavigationDecisionProvider provider, string state,
            IReadOnlyList<NavigationPhrase> candidates, CancellationToken cancellation, bool transmission)
        {
            var result = transmission ? await provider.ChooseGuidanceAsync(state, candidates, cancellation) :
                new GuidanceSelection { Phrase = await provider.ChooseAsync(state, candidates, cancellation), Speak = true };
            cancellation.ThrowIfCancellationRequested();
            if (result != null)
                foreach (var candidate in candidates)
                    if (candidate.id == result.Phrase.id && candidate.text == result.Phrase.text) return result;
            throw new FormatException("Provider choice is outside the offered candidate set.");
        }
    }
}
