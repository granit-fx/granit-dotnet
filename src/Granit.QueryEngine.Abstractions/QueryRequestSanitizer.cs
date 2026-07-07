using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;

namespace Granit.QueryEngine;

/// <summary>
/// Untrusted query input to be validated against a <see cref="QueryMetadata"/> whitelist by
/// <see cref="QueryRequestSanitizer.Sanitize"/>. Carries the raw candidate values exactly as an
/// untrusted producer (LLM output, tool arguments) supplied them.
/// </summary>
public sealed record QueryRequestCandidate
{
    /// <summary>Requested one-based page number, if any.</summary>
    public int? Page { get; init; }

    /// <summary>Requested page size, if any.</summary>
    public int? PageSize { get; init; }

    /// <summary>Requested sort specification (comma-separated, <c>-</c> prefix for descending).</summary>
    public string? Sort { get; init; }

    /// <summary>Requested filter clauses as <c>"field.operator"</c> / value pairs (may contain duplicates).</summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Filter { get; init; }

    /// <summary>Requested quick-filter names.</summary>
    public IReadOnlyList<string>? QuickFilters { get; init; }

    /// <summary>Requested group-by field.</summary>
    public string? GroupBy { get; init; }

    /// <summary>Free-text search term (not whitelist-constrained; the engine scopes it to declared search properties).</summary>
    public string? Search { get; init; }
}

/// <summary>
/// Outcome of <see cref="QueryRequestSanitizer.Sanitize"/>: the whitelisted
/// <see cref="QueryRequest"/> plus the filter keys that were dropped because the definition
/// does not expose them.
/// </summary>
/// <param name="Request">The sanitized request, safe to hand to the query engine.</param>
/// <param name="IgnoredFilters">Filter keys rejected by the whitelist (useful for feedback to the producer).</param>
public sealed record QueryRequestSanitizationResult(
    QueryRequest Request,
    IReadOnlyList<string> IgnoredFilters);

/// <summary>
/// Single whitelist gate between untrusted structured query input and the query engine
/// (CWE-20 / OWASP LLM02). Every producer of machine-generated <see cref="QueryRequest"/>s —
/// the NLQ translator, the <c>query_data</c> agent tool, or any future channel — MUST route
/// its candidate through <see cref="Sanitize"/> so field/operator whitelisting and pagination
/// clamping cannot drift between code paths.
/// </summary>
public static class QueryRequestSanitizer
{
    /// <summary>
    /// Validates <paramref name="candidate"/> against <paramref name="metadata"/>: non-whitelisted
    /// filter clauses are dropped (first occurrence wins on duplicate keys), sort segments and
    /// group-by/quick-filter names are stripped when not declared, the page number is floored at 1,
    /// and the page size is clamped to the definition's
    /// <see cref="PaginationMeta.MaxPageSize"/>.
    /// </summary>
    /// <param name="candidate">The untrusted candidate values.</param>
    /// <param name="metadata">The query definition metadata acting as the whitelist.</param>
    /// <returns>The sanitized request and the rejected filter keys.</returns>
    public static QueryRequestSanitizationResult Sanitize(
        QueryRequestCandidate candidate,
        QueryMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(metadata);

        var allowedFilterKeys = metadata.FilterableFields
            .SelectMany(f => f.Operators.Select(op => $"{f.Name}.{OperatorCode(op)}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, string>? filter = null;
        List<string>? ignored = null;

        if (candidate.Filter is { Count: > 0 })
        {
            filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string value) in candidate.Filter)
            {
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!allowedFilterKeys.Contains(key))
                {
                    (ignored ??= []).Add(key);
                    continue;
                }

                // First occurrence wins on duplicate keys.
                filter.TryAdd(key, value);
            }

            if (filter.Count == 0)
            {
                filter = null;
            }
        }

        return new QueryRequestSanitizationResult(
            new QueryRequest
            {
                Page = candidate.Page is >= 1 ? candidate.Page : null,
                PageSize = ClampPageSize(candidate.PageSize, metadata.Pagination),
                Sort = SanitizeSort(candidate.Sort, metadata),
                Filter = filter,
                QuickFilters = SanitizeQuickFilters(candidate.QuickFilters, metadata),
                GroupBy = SanitizeGroupBy(candidate.GroupBy, metadata),
                Search = string.IsNullOrWhiteSpace(candidate.Search) ? null : candidate.Search,
            },
            ignored ?? (IReadOnlyList<string>)[]);
    }

    /// <summary>
    /// Canonical wire code of a <see cref="FilterOperator"/> inside a <c>"field.operator"</c>
    /// filter key (the lowercase enum name, e.g. <c>eq</c>, <c>gte</c>, <c>contains</c>).
    /// </summary>
    /// <param name="op">The filter operator.</param>
    /// <returns>The lowercase operator code.</returns>
    public static string OperatorCode(FilterOperator op) =>
        op.ToString().ToLowerInvariant();

    private static int? ClampPageSize(int? requested, PaginationMeta pagination)
    {
        if (requested is not { } size)
        {
            return null;
        }

        return Math.Clamp(size, 1, pagination.MaxPageSize);
    }

    private static string? SanitizeSort(string? sort, QueryMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(sort))
        {
            return null;
        }

        var allowedSortFields = metadata.SortableFields
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] validParts = sort
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => allowedSortFields.Contains(p.StartsWith('-') ? p[1..] : p))
            .ToArray();

        return validParts.Length > 0 ? string.Join(',', validParts) : null;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<string>? SanitizeQuickFilters(
        IReadOnlyList<string>? quickFilters, QueryMetadata metadata)
    {
        if (quickFilters is not { Count: > 0 })
        {
            return null;
        }

        var allowed = metadata.QuickFilters
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var valid = quickFilters.Where(allowed.Contains).ToList();
        return valid.Count > 0 ? valid.AsReadOnly() : null;
    }

    private static string? SanitizeGroupBy(string? groupBy, QueryMetadata metadata) =>
        !string.IsNullOrWhiteSpace(groupBy)
        && metadata.GroupByFields.Any(f => f.Name.Equals(groupBy, StringComparison.OrdinalIgnoreCase))
            ? groupBy
            : null;
}
