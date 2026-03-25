using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.EntraId.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.EntraId;

/// <summary>
/// Granit module for Entra ID extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitJwtBearerModule))]
public sealed class GranitAuthenticationEntraIdModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntraId();
}
