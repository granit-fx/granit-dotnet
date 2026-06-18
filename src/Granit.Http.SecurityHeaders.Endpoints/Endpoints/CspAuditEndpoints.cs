using Granit.Diagnostics.Endpoints.Permissions;
using Granit.Http.SecurityHeaders.Endpoints.Dtos;
using Granit.Http.SecurityHeaders.Internal;
using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Endpoints.Endpoints;

/// <summary>
/// CSP audit endpoint — returns the effective per-endpoint
/// <c>Content-Security-Policy</c> snapshot for security auditors.
/// </summary>
internal static class CspAuditEndpoints
{
    internal static void Map(RouteGroupBuilder group)
    {
        group.MapGet("csp", HandleGetCspAudit)
            .RequireAuthorization(DiagnosticsPermissions.Monitoring.Read)
            .WithName("GetCspAudit")
            .WithSummary("Returns the effective Content-Security-Policy per endpoint.")
            .WithDescription(
                "Returns a snapshot of the composed CSP for every registered endpoint, " +
                "plus the base directives and the list of registered ICspContributor " +
                "implementations. Contributors that branch on request-scoped state beyond " +
                "endpoint metadata (e.g. authenticated user, tenant) will under-report — the " +
                "audit synthesises requests carrying only the matched endpoint.")
            .Produces<CspAuditResponse>();
    }

    internal static Ok<CspAuditResponse> HandleGetCspAudit(
        [FromServices] EndpointDataSource endpointDataSource,
        [FromServices] ICspContributorRegistry registry,
        [FromServices] CspComposer composer,
        [FromServices] IOptionsMonitor<GranitSecurityHeadersOptions> optionsMonitor)
    {
        CspOptions csp = optionsMonitor.CurrentValue.Csp;

        IReadOnlyDictionary<string, IReadOnlyCollection<string>> baseDirectives =
            BuildBaseDirectives(csp);

        IReadOnlyList<CspContributorInfo> contributors =
        [
            .. registry.Contributors.Select(c =>
                new CspContributorInfo(c.Name, c.GetType().FullName ?? c.GetType().Name)),
        ];

        IReadOnlyList<CspEndpointAudit> endpoints = [.. endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(ep => AuditEndpoint(ep, composer))];

        CspAuditResponse response = new(
            BaseDirectives: baseDirectives,
            RawOverride: csp.RawOverride,
            ReportOnly: csp.ReportOnly,
            Contributors: contributors,
            Endpoints: endpoints);

        return TypedResults.Ok(response);
    }

    private static Dictionary<string, IReadOnlyCollection<string>> BuildBaseDirectives(
        CspOptions csp)
    {
        Dictionary<string, IReadOnlyCollection<string>> result = new(StringComparer.Ordinal);

        AddIfPresent(result, "default-src", csp.DefaultSrc);
        AddIfPresent(result, "script-src", csp.ScriptSrc);
        AddIfPresent(result, "script-src-elem", csp.ScriptSrcElem);
        AddIfPresent(result, "script-src-attr", csp.ScriptSrcAttr);
        AddIfPresent(result, "style-src", csp.StyleSrc);
        AddIfPresent(result, "style-src-elem", csp.StyleSrcElem);
        AddIfPresent(result, "style-src-attr", csp.StyleSrcAttr);
        AddIfPresent(result, "font-src", csp.FontSrc);
        AddIfPresent(result, "img-src", csp.ImgSrc);
        AddIfPresent(result, "connect-src", csp.ConnectSrc);
        AddIfPresent(result, "frame-src", csp.FrameSrc);
        AddIfPresent(result, "worker-src", csp.WorkerSrc);
        AddIfPresent(result, "media-src", csp.MediaSrc);
        AddIfPresent(result, "object-src", csp.ObjectSrc);
        AddIfPresent(result, "manifest-src", csp.ManifestSrc);
        AddIfPresent(result, "child-src", csp.ChildSrc);
        AddIfPresent(result, "base-uri", csp.BaseUri);
        AddIfPresent(result, "form-action", csp.FormAction);
        AddIfPresent(result, "frame-ancestors", csp.FrameAncestors);

        return result;
    }

    private static void AddIfPresent(
        Dictionary<string, IReadOnlyCollection<string>> map,
        string directive,
        IList<string> sources)
    {
        if (sources is { Count: > 0 })
        {
            map[directive] = [.. sources];
        }
    }

    private static CspEndpointAudit AuditEndpoint(RouteEndpoint endpoint, CspComposer composer)
    {
        DefaultHttpContext synthetic = new();
        synthetic.SetEndpoint(endpoint);

        (string Name, string Value)? composed = composer.Compose(synthetic);

        IReadOnlyList<string> methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods
            ?? (IReadOnlyList<string>)Array.Empty<string>();

        return new CspEndpointAudit(
            HttpMethods: methods,
            Pattern: endpoint.RoutePattern.RawText ?? string.Empty,
            DisplayName: endpoint.DisplayName ?? string.Empty,
            HeaderName: composed?.Name ?? string.Empty,
            ComposedCsp: composed?.Value ?? string.Empty);
    }
}
