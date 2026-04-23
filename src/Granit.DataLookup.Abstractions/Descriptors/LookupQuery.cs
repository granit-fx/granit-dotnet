namespace Granit.DataLookup.Descriptors;

/// <summary>
/// Query parameters passed to an <see cref="Sources.ILookupSource"/> when searching.
/// </summary>
/// <param name="Search">Optional typeahead search term.</param>
/// <param name="Page">1-based page index. Ignored by cursor-based sources.</param>
/// <param name="PageSize">Items per page. Clamped to the source's allowed range.</param>
/// <param name="Scope">
/// Fully resolved scope values keyed by the names the source declared in
/// <see cref="LookupDescriptor.ScopeKeys"/>. The source MUST validate that every declared
/// key is present and non-empty before executing the query (400 otherwise).
/// </param>
/// <param name="ContinuationToken">
/// Opaque token returned by the previous page. Used by cursor-based sources.
/// </param>
public sealed record LookupQuery(
    string? Search = null,
    int Page = 1,
    int PageSize = 25,
    IReadOnlyDictionary<string, string?>? Scope = null,
    string? ContinuationToken = null);
