using Granit.Authentication.JwtBearer.Extensions;
using Granit.Caching;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer;

/// <summary>
/// Granit module for generic OIDC JWT Bearer authentication.
/// Depends on <see cref="GranitModule"/> for the abstractions.
/// </summary>
[DependsOn(typeof(GranitCachingModule))]
public sealed class GranitAuthenticationJwtBearerModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitJwtBearer();
}
