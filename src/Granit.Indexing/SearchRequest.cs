namespace Granit.Indexing;

/// <summary>
/// Read-side request handed to <see cref="ISearchService{TKey, TResult}"/>.
/// </summary>
/// <param name="Query">
/// Free-text natural-language query. Treated as a phrase by default; backends MUST NOT
/// interpret operator syntax unless the caller is explicitly authorised for advanced
/// search (see <paramref name="UseAdvancedSyntax"/>).
/// </param>
/// <param name="Page">1-based page index.</param>
/// <param name="PageSize">Items per page; bounded by <c>GranitIndexingOptions.MaxPageSize</c>.</param>
/// <param name="Language">
/// Optional ISO 639-1 language hint. When omitted, backends use the per-entry language
/// stored at index time; this override is useful when callers know the corpus is mono-
/// lingual or want to force a specific analyser.
/// </param>
/// <param name="PrincipalIdentifier">
/// Stable identifier of the calling principal — used as the bucket key for the empty-
/// result rate limiter. The framework hashes it before persistence/log emission so the
/// raw value never crosses the trust boundary. Endpoint layer typically passes
/// <c>User.GetSubjectId()</c> (OIDC <c>sub</c>) or equivalent. <c>null</c> disables the
/// limiter for the call (server-to-server scenarios).
/// </param>
/// <param name="UseAdvancedSyntax">
/// When <c>true</c>, backends MAY interpret operator syntax (e.g. Postgres
/// <c>to_tsquery</c>, ES query string). Endpoints MUST gate this on a dedicated
/// <c>Search.Advanced.Execute</c> permission before forwarding the request — accepting
/// raw operators from anonymous traffic re-opens the tsquery-injection attack surface.
/// </param>
public sealed record SearchRequest(
    string Query,
    int Page = 1,
    int PageSize = 20,
    string? Language = null,
    string? PrincipalIdentifier = null,
    bool UseAdvancedSyntax = false);
