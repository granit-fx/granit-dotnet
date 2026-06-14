namespace Granit.AI.Chat.Suggestions;

/// <summary>
/// Module-implemented hook that contributes typed <see cref="AISuggestedAction"/> for a turn
/// (ADR-067). A module registers a provider to surface its own suggestion types — e.g. a calendar
/// module proposing "connect a calendar" when none is linked. Providers are resolved per scope so
/// they run under the caller's identity and ACLs, and must return only suggestions the caller may
/// see and act on.
/// </summary>
/// <remarks>
/// Return an empty list when nothing applies. Suggestions are declarative deep links only — a
/// provider must never perform the action it suggests (the non-executing guarantee, v1).
/// </remarks>
public interface IAISuggestionProvider
{
    /// <summary>Returns the suggestions this provider offers for the turn, or an empty list.</summary>
    /// <param name="context">The turn context (owner and message).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
        AISuggestionContext context,
        CancellationToken cancellationToken = default);
}
