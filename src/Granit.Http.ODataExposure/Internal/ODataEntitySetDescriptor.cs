namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Captures one EntitySet registration: the route segment, the CLR entity
/// type, the <c>QueryDefinition&lt;TEntity&gt;</c> CLR type, the optional
/// permission gate, and the per-set query-hardening caps from C3 (#1392).
/// Built up by <see cref="Options.ODataExposureOptions"/> at host
/// configuration time, consumed by the route helper to wire one minimal-API
/// endpoint per set + the shared EDM model.
/// </summary>
/// <param name="EntitySetName">Route segment AND OData EntitySet name (e.g. <c>"Invoices"</c>).</param>
/// <param name="EntityType">CLR type of the entity.</param>
/// <param name="QueryDefinitionType">CLR type of the <c>QueryDefinition&lt;TEntity&gt;</c> backing this set — resolved through DI at request time.</param>
/// <param name="RequiredPermission">Permission name a request must carry to read this EntitySet, or <see langword="null"/> if any authenticated user is allowed (auth itself is enforced by the surrounding pipeline).</param>
/// <param name="MaxTop">Server-side cap on <c>$top</c>. Requests above this are silently clamped and an <c>OData-MaxTop-Applied</c> header is set on the response. Default <c>5000</c>.</param>
/// <param name="PageSize">Server-side default page size when the caller omits <c>$top</c>. Drives <c>@odata.nextLink</c> pagination. Default <c>1000</c>.</param>
/// <param name="CountEnabled"><c>$count=true</c> requests are accepted only when this is <see langword="true"/>. Default <see langword="false"/> — guards huge tables against full-table-scan counts on every refresh.</param>
/// <param name="ExpandWhitelist">Allowed top-level navigation properties for <c>$expand</c>. <see langword="null"/> means <c>$expand</c> is disabled entirely. Empty list (<c>[]</c>) ALSO disables expand — the EntitySet's contract is "no navigation by default; opt in explicitly per property".</param>
/// <param name="MaxExpansionDepth">Maximum nesting depth allowed for <c>$expand</c>. Default <c>1</c> — flat expand only; nested expand requires explicit opt-in.</param>
/// <param name="AnonymousAccessAcknowledged">Set by <see cref="Options.ODataEntitySetBuilder{TEntity}.AllowAnonymousAccess"/> to declare that the absence of <see cref="RequiredPermission"/> is intentional. Without this flag, the strict-config validator (C6 #1395) refuses to start the host — every OData EntitySet must be either gated or explicitly anonymous.</param>
/// <param name="ExpandConfigurationAcknowledged">Set by <see cref="Options.ODataEntitySetBuilder{TEntity}.ExpandWhitelist"/>, <see cref="Options.ODataEntitySetBuilder{TEntity}.DisableExpand"/>, or implicit via <see cref="Options.ODataEntitySetBuilder{TEntity}.AllowNoExpansion"/>. The strict-config validator refuses to start the host until one of these is called — implicit "expand disabled" is a security smell, the host must declare the intent.</param>
internal sealed record ODataEntitySetDescriptor(
    string EntitySetName,
    Type EntityType,
    Type QueryDefinitionType,
    string? RequiredPermission,
    int MaxTop = 5000,
    int PageSize = 1000,
    bool CountEnabled = false,
    IReadOnlyList<string>? ExpandWhitelist = null,
    int MaxExpansionDepth = 1,
    bool AnonymousAccessAcknowledged = false,
    bool ExpandConfigurationAcknowledged = false);
