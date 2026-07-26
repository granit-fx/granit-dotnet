using Granit.Authentication.Mtls.Diagnostics;
using Granit.Authentication.Mtls.Extensions;
using Granit.Diagnostics;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.Mtls;

/// <summary>
/// Granit module for mutual-TLS certificate-bound token validation (RFC 8705).
/// IdP-agnostic — works with OpenIddict, Keycloak, Entra ID, Auth0, or any OIDC provider that issues
/// <c>cnf.x5t#S256</c>-bound access tokens.
/// </summary>
public sealed class GranitAuthenticationMtlsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitMtlsValidation();
        context.Services.TryAddSingleton<MtlsValidationMetrics>();
        GranitActivitySourceRegistry.Register(MtlsValidationActivitySource.Name);
    }
}
