using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;

namespace Granit.Mentions.Internal;

/// <summary>
/// Exposes every opted-in <see cref="IMentionResolver"/> as a single <see cref="ILookupSource"/>
/// named <c>mentions</c>, so the <c>@</c> picker rides on <c>Granit.DataLookup</c>
/// (<c>GET /lookups/mentions</c>) with no changes to that framework. Fans out the query across the
/// resolvers (optionally narrowed to one <c>type</c> via <see cref="LookupQuery.Scope"/>), applies
/// lenient per-type authorization — an unauthorized type is silently skipped, never a 403 for the
/// whole picker — merges and caps the suggestions, and encodes the chosen reference as a composite
/// <c>type:id</c> value so a single source can resolve any mention type.
/// </summary>
internal sealed class MentionLookupSource(IMentionRegistry registry, IMentionAuthorizer authorizer)
    : ILookupSource
{
    /// <summary>The registry key under which the facade is exposed.</summary>
    public const string SourceName = "mentions";

    /// <summary>Optional <see cref="LookupQuery.Scope"/> key narrowing the search to one type.</summary>
    public const string TypeScopeKey = "type";

    private const char Separator = ':';

    public string Name => SourceName;

    /// <summary>Public to any caller who may use mentions; each type's own permission is checked internally.</summary>
    public string? RequiredPermission => null;

    /// <summary><c>type</c> is an optional filter, not a required scope.</summary>
    public IReadOnlyList<string> ScopeKeys => [];

    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int limit = query.PageSize;
        if (limit <= 0)
        {
            return new LookupResult([]);
        }

        string search = query.Search ?? string.Empty;
        string? type = null;
        query.Scope?.TryGetValue(TypeScopeKey, out type);

        List<LookupItem> items = [];
        foreach (IMentionResolver resolver in SelectResolvers(type))
        {
            if (!await IsAuthorizedAsync(resolver, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            IReadOnlyList<MentionSuggestion> suggestions =
                await resolver.SearchAsync(search, limit, cancellationToken).ConfigureAwait(false);
            foreach (MentionSuggestion suggestion in suggestions)
            {
                items.Add(ToItem(resolver.Type, suggestion));
            }

            if (items.Count >= limit)
            {
                break;
            }
        }

        return new LookupResult(items.Count > limit ? items[..limit] : items);
    }

    public async ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryDecode(value.ToString(), out string type, out string id)
            || !registry.TryGet(type, out IMentionResolver? resolver)
            || !await IsAuthorizedAsync(resolver, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        MentionTarget? target = await resolver.ResolveAsync(id, cancellationToken).ConfigureAwait(false);
        return target is null
            ? null
            : new LookupItem(
                Encode(target.Type, target.Id),
                target.Label,
                new Dictionary<string, object?> { ["type"] = target.Type, ["content"] = target.Content });
    }

    private IReadOnlyList<IMentionResolver> SelectResolvers(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return registry.Resolvers;
        }

        return registry.TryGet(type, out IMentionResolver? resolver) ? [resolver] : [];
    }

    private ValueTask<bool> IsAuthorizedAsync(IMentionResolver resolver, CancellationToken cancellationToken) =>
        authorizer.IsAuthorizedAsync(resolver, cancellationToken);

    private static LookupItem ToItem(string type, MentionSuggestion suggestion)
    {
        Dictionary<string, object?> extra = new() { ["type"] = type };
        if (suggestion.Description is not null)
        {
            extra["description"] = suggestion.Description;
        }

        return new LookupItem(Encode(type, suggestion.Id), suggestion.Label, extra);
    }

    private static string Encode(string type, string id) => $"{type}{Separator}{id}";

    private static bool TryDecode(string? composite, out string type, out string id)
    {
        type = string.Empty;
        id = string.Empty;
        if (string.IsNullOrEmpty(composite))
        {
            return false;
        }

        int separator = composite.IndexOf(Separator, StringComparison.Ordinal);
        if (separator <= 0 || separator == composite.Length - 1)
        {
            return false;
        }

        type = composite[..separator];
        id = composite[(separator + 1)..];
        return true;
    }
}
