using Granit.Http.SecurityHeaders;

namespace Granit.Diagnostics.Endpoints.Dtos;

/// <summary>
/// Snapshot returned by the CSP audit endpoint.
/// </summary>
/// <param name="BaseDirectives">
/// The seeded CSP directives from <c>CspOptions</c> (before any contributor
/// runs).
/// </param>
/// <param name="RawOverride">
/// Non-null when <c>CspOptions.RawOverride</c> bypasses composition. The
/// raw string is emitted verbatim and no contributor is consulted.
/// </param>
/// <param name="ReportOnly">
/// <c>true</c> when the composer emits <c>Content-Security-Policy-Report-Only</c>
/// instead of the enforcing header.
/// </param>
/// <param name="Contributors">
/// Registered <see cref="ICspContributor"/> instances by name and full type.
/// Order matches registration order.
/// </param>
/// <param name="Endpoints">
/// Per-matched-endpoint composed CSP. Synthesised by running the composer
/// with an <see cref="Microsoft.AspNetCore.Http.HttpContext"/> carrying
/// only the matched endpoint — contributors that branch on request-scoped
/// state beyond endpoint metadata will under-report here.
/// </param>
public sealed record CspAuditResponse(
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> BaseDirectives,
    string? RawOverride,
    bool ReportOnly,
    IReadOnlyList<CspContributorInfo> Contributors,
    IReadOnlyList<CspEndpointAudit> Endpoints);

/// <summary>Identification of a registered CSP contributor.</summary>
public sealed record CspContributorInfo(string Name, string FullTypeName);

/// <summary>
/// Composed CSP for one endpoint.
/// </summary>
/// <param name="HttpMethods">
/// HTTP methods accepted by the endpoint (e.g. <c>["GET"]</c>). Empty when
/// the endpoint accepts any method.
/// </param>
/// <param name="Pattern">The route pattern (e.g. <c>"/scalar"</c>).</param>
/// <param name="DisplayName">The endpoint display name from routing metadata.</param>
/// <param name="HeaderName">
/// Either <c>"Content-Security-Policy"</c> or
/// <c>"Content-Security-Policy-Report-Only"</c>.
/// </param>
/// <param name="ComposedCsp">The composed header value.</param>
public sealed record CspEndpointAudit(
    IReadOnlyList<string> HttpMethods,
    string Pattern,
    string DisplayName,
    string HeaderName,
    string ComposedCsp);
