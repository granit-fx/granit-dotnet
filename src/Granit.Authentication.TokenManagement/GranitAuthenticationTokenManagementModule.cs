using Granit.Authentication.Oidc;
using Granit.Authentication.TokenManagement.Extensions;
using Granit.Core.Modularity;
using Granit.Timing;

namespace Granit.Authentication.TokenManagement;

/// <summary>
/// Granit module for OAuth 2.0 token lifecycle management.
/// Registers <see cref="Services.ITokenEndpointService"/>, <see cref="Services.ITokenRevocationService"/>,
/// and <see cref="Cache.IClientCredentialsTokenCache"/> as singletons.
/// </summary>
[DependsOn(
    typeof(GranitAuthenticationOidcModule),
    typeof(GranitTimingModule))]
public sealed class GranitAuthenticationTokenManagementModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTokenManagement();
}
