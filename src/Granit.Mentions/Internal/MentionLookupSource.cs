using Granit.Authorization;
using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Registry;
using Granit.DataLookup.Sources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Mentions.Internal;

/// <summary>
/// The <c>mentions</c> facade <see cref="ILookupSource"/>: it exposes every lookup source tagged
/// mentionable (<see cref="MentionSource"/>) through one source, so the <c>@</c> picker rides on
/// <c>Granit.DataLookup</c> (<c>GET /lookups/mentions</c>) with no changes to that framework and no
/// mention-specific contract. A mention type is simply a lookup source opted into the picker.
/// </summary>
/// <remarks>
/// Fans the query out across the tagged sources (optionally narrowed to one <c>type</c> via
/// <see cref="LookupQuery.Scope"/>), applies lenient per-type authorization — an unauthorized type
/// is silently skipped, never a 403 for the whole picker — merges and caps, and re-stamps each
/// item's value as a composite <c>type:value</c> so a single source resolves any type.
/// </remarks>
internal sealed partial class MentionLookupSource(
    IEnumerable<MentionSource> mentionSources,
    IServiceProvider serviceProvider,
    IPermissionChecker permissionChecker,
    ILogger<MentionLookupSource> logger) : ILookupSource
{
    private const char Separator = ':';

    // Resolved lazily, not constructor-injected: LookupRegistry consumes IEnumerable<ILookupSource>,
    // which includes this source, so a constructor dependency on ILookupRegistry forms a DI cycle.
    // Both this source and the registry are scoped, so by the time SearchAsync runs this instance is
    // already cached in the scope — resolving the registry here reuses it, no recursion. (#2833)
    private ILookupRegistry Registry => serviceProvider.GetRequiredService<ILookupRegistry>();

    public string Name => MentionLookup.SourceName;

    /// <summary>Public to any caller who may use mentions; each type's own permission is checked internally.</summary>
    public string? RequiredPermission => null;

    /// <summary><c>type</c> is an optional filter, not a required scope.</summary>
    public IReadOnlyList<string> ScopeKeys => [];

    private IEnumerable<string> MentionableNames =>
        mentionSources.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        int limit = query.PageSize;
        if (limit <= 0)
        {
            return new LookupResult([]);
        }

        string? type = null;
        query.Scope?.TryGetValue(MentionLookup.TypeScopeKey, out type);
        var inner = new LookupQuery(query.Search, Page: 1, PageSize: limit);

        List<LookupItem> items = [];
        foreach (string name in SelectNames(type))
        {
            ILookupSource? source = Registry.Resolve(name);
            if (source is null || !await IsAuthorizedAsync(source, cancellationToken).ConfigureAwait(false))
            {
                LogSourceSkipped(name);
                continue;
            }

            LookupResult result = await source.SearchAsync(inner, cancellationToken).ConfigureAwait(false);
            foreach (LookupItem item in result.Items)
            {
                items.Add(Restamp(name, item));
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

        if (!TryDecode(value.ToString(), out string type, out string inner)
            || !MentionableNames.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        ILookupSource? source = Registry.Resolve(type);
        if (source is null || !await IsAuthorizedAsync(source, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        LookupItem? item = await source.ResolveByValueAsync(inner, cancellationToken).ConfigureAwait(false);
        return item is null ? null : Restamp(type, item);
    }

    private IEnumerable<string> SelectNames(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return MentionableNames;
        }

        return MentionableNames.Contains(type, StringComparer.OrdinalIgnoreCase) ? [type] : [];
    }

    private async ValueTask<bool> IsAuthorizedAsync(ILookupSource source, CancellationToken cancellationToken)
    {
        string? permission = source.RequiredPermission;
        return string.IsNullOrWhiteSpace(permission)
            || await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false);
    }

    private static LookupItem Restamp(string type, LookupItem item)
    {
        Dictionary<string, object?> extra = item.Extra is null
            ? []
            : new Dictionary<string, object?>(item.Extra);
        extra[MentionLookup.TypeScopeKey] = type;
        return new LookupItem($"{type}{Separator}{item.Value}", item.Label, extra);
    }

    private static bool TryDecode(string? composite, out string type, out string inner)
    {
        type = string.Empty;
        inner = string.Empty;
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
        inner = composite[(separator + 1)..];
        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Mention type '{Type}' was skipped from the picker: the source is unregistered or the caller lacks its required permission.")]
    private partial void LogSourceSkipped(string type);
}
