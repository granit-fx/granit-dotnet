using Granit.Caching;
using Granit.Modularity;
using Granit.Oidc;
using Granit.Oidc.TokenManagement.Extensions;
using Granit.Timing;

namespace Granit.Oidc.TokenManagement;

/// <summary>
/// Granit module for OAuth 2.0 token lifecycle management.
/// Registers <see cref="Services.ITokenEndpointService"/>, <see cref="Services.ITokenRevocationService"/>,
/// and <see cref="Cache.IClientCredentialsTokenCache"/> as singletons.
/// </summary>
[DependsOn(
    typeof(GranitCachingModule),
    typeof(GranitOidcModule),
    typeof(GranitTimingModule))]
public sealed class GranitOidcTokenManagementModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTokenManagement();
}
