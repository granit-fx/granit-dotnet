using Granit.AI.Chat.Mentions;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAIMentionSearchService"/>. Dispatches a picker query to the opted-in
/// resolvers under the caller's ACLs, merges their suggestions, and caps the result.
/// </summary>
internal sealed class AIMentionSearchService(IAIMentionRegistry registry) : IAIMentionSearchService
{
    public async ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchAsync(
        string query, string? type = null, int limit = 8, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        if (type is not null)
        {
            return registry.TryGet(type, out IAIMentionResolver? resolver)
                ? await SearchOneAsync(resolver, query, limit, cancellationToken).ConfigureAwait(false)
                : [];
        }

        List<AIMentionSuggestion> merged = [];
        foreach (IAIMentionResolver resolver in registry.Resolvers)
        {
            IReadOnlyList<AIMentionSuggestion> suggestions =
                await SearchOneAsync(resolver, query, limit, cancellationToken).ConfigureAwait(false);
            merged.AddRange(suggestions);
            if (merged.Count >= limit)
            {
                break;
            }
        }

        return merged.Count > limit ? merged[..limit] : merged;
    }

    private static async ValueTask<IReadOnlyList<AIMentionSuggestion>> SearchOneAsync(
        IAIMentionResolver resolver, string query, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<AIMentionSuggestion> suggestions =
            await resolver.SearchAsync(query, limit, cancellationToken).ConfigureAwait(false);

        // A resolver echoes its own Type on each suggestion; trust the registry's key instead so a
        // mistyped suggestion can never be dispatched to the wrong resolver on resolve.
        return [.. suggestions.Select(s => s with { Type = resolver.Type })];
    }
}
