using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Granit.Indexing;

/// <summary>
/// Page of authorised search hits returned by <see cref="ISearchService{TKey, TResult}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Transparency contract.</b>
/// <see cref="HitAuthorizationLimit"/> is an aggregated UX hint, surfaced after the
/// orchestrator gave up the over-fetch loop on <c>MaxAuthorizationDepth</c>. It is NOT
/// a per-query existence oracle: endpoint adapters MUST throttle the hint to at most
/// one display per principal per 60 s. The framework cannot enforce that throttle
/// (it has no UI state) so the obligation lives in the consumer endpoint layer.
/// </para>
/// <para>
/// <b>BackendHitCount is internal.</b> <see cref="BackendHitCount"/> is marked
/// <see cref="JsonIgnoreAttribute"/> and <see cref="EditorBrowsableAttribute"/>
/// <c>Never</c> so it never round-trips through HTTP serialisation. Its only legitimate
/// consumer is telemetry — <see cref="Diagnostics.IndexingMetrics"/> emits it under the
/// <c>granit.indexing.search.backend_hit_count</c> counter (tenant-tagged, never
/// principal-tagged). An architecture test in I-F5.2 forbids cross-package access from
/// <c>.Endpoints</c> projects.
/// </para>
/// </remarks>
public sealed record SearchPage<TResult>
{
    /// <summary>Authorised items for this page.</summary>
    public required IReadOnlyList<TResult> Items { get; init; }

    /// <summary>1-based page index this response covers.</summary>
    public required int Page { get; init; }

    /// <summary>Page size this response was sized for.</summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// Count of items in <see cref="Items"/>. Equals
    /// <c>Min(PageSize, total authorised so far)</c>. Callers MUST NOT infer a tenant-
    /// wide row count from this value — see <see cref="HitAuthorizationLimit"/>.
    /// </summary>
    public required int TotalAuthorized { get; init; }

    /// <summary>
    /// <c>true</c> when the orchestrator exhausted
    /// <see cref="Options.GranitIndexingOptions.MaxAuthorizationDepth"/> before filling
    /// the page. Aggregated UX hint only — see remarks.
    /// </summary>
    public bool HitAuthorizationLimit { get; init; }

    /// <summary>
    /// Total backend rows scanned across all over-fetch iterations of this query.
    /// <b>Internal / telemetry-only.</b> Never returned through HTTP.
    /// </summary>
    [JsonIgnore]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int BackendHitCount { get; init; }

}
