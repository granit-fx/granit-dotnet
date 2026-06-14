namespace Granit.AI.Chat.Suggestions;

/// <summary>
/// Gathers the typed suggested actions for a turn from every registered
/// <see cref="IAISuggestionProvider"/>, under the caller's ACLs, de-duplicated by
/// <see cref="AISuggestedAction.Type"/>.
/// </summary>
public interface IAISuggestionResolver
{
    /// <summary>
    /// Collects the suggestions every provider offers for <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The turn context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined, de-duplicated suggestions (possibly empty).</returns>
    ValueTask<IReadOnlyList<AISuggestedAction>> ResolveAsync(
        AISuggestionContext context,
        CancellationToken cancellationToken = default);
}
