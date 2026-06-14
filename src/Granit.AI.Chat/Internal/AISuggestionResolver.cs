using Granit.AI.Chat.Suggestions;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAISuggestionResolver"/>. Fans out to every registered
/// <see cref="IAISuggestionProvider"/> and concatenates their suggestions, keeping the first of
/// each <see cref="AISuggestedAction.Type"/>. A provider that throws is skipped so one module
/// cannot break the turn.
/// </summary>
internal sealed class AISuggestionResolver(IEnumerable<IAISuggestionProvider> providers) : IAISuggestionResolver
{
    public async ValueTask<IReadOnlyList<AISuggestedAction>> ResolveAsync(
        AISuggestionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AISuggestedAction>? collected = null;
        HashSet<string>? seenTypes = null;

        foreach (IAISuggestionProvider provider in providers)
        {
            IReadOnlyList<AISuggestedAction> suggestions;
            try
            {
                suggestions = await provider.GetSuggestionsAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // A misbehaving suggestion provider must never fail the turn — its answer stands.
                continue;
            }

            foreach (AISuggestedAction suggestion in suggestions)
            {
                collected ??= [];
                seenTypes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (seenTypes.Add(suggestion.Type))
                {
                    collected.Add(suggestion);
                }
            }
        }

        return collected ?? [];
    }
}
