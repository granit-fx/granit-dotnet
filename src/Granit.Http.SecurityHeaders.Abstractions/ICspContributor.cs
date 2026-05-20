using Microsoft.AspNetCore.Http;

namespace Granit.Http.SecurityHeaders;

/// <summary>
/// Contributes <c>Content-Security-Policy</c> source expressions for requests
/// that match this contributor's scope.
/// </summary>
/// <remarks>
/// <para>
/// Implementations decide applicability per-request, typically by inspecting
/// <c>context.GetEndpoint()?.Metadata</c> for a marker type the contributor's
/// owning package attaches via <c>.WithMetadata(...)</c> at endpoint mapping
/// time. Contributors that don't apply must no-op (do not throw, do not
/// mutate the builder).
/// </para>
/// <para>
/// <b>Purity contract.</b> Implementations MUST behave as a pure function of
/// the matched <see cref="Microsoft.AspNetCore.Http.Endpoint"/> — all
/// branching MUST be on
/// <c>context.GetEndpoint()?.Metadata</c> or compile-time constants. Branching
/// on the authenticated user, request headers, tenant, or any other
/// request-scoped state is forbidden: the composer caches the composed CSP
/// per endpoint, so request-scoped variation would leak across
/// tenants/users.
/// </para>
/// </remarks>
public interface ICspContributor
{
    /// <summary>
    /// Stable identifier used by
    /// <c>GranitSecurityHeadersOptions.DisabledContributors</c> to opt out of
    /// this contributor from configuration. Defaults to the runtime type
    /// name; override only when the type name is not stable across refactors.
    /// </summary>
    string Name => GetType().Name;

    /// <summary>
    /// Adds source expressions to <paramref name="builder"/> for the current
    /// request. No-op when the contributor does not apply to this request.
    /// </summary>
    /// <param name="context">The current request — read-only for inspection.
    /// Do not mutate.</param>
    /// <param name="builder">The per-request CSP builder. Append sources via
    /// the <c>Add*</c>/<c>Set*</c> fluent methods. The builder is shared with
    /// other matching contributors and read by the composer after every
    /// contributor has run.</param>
    void Contribute(HttpContext context, CspBuilder builder);
}
