using Granit.Authentication.JwtBearer;
using Granit.Authentication.JwtBearer.EntraId.Extensions;
using Granit.Modularity;

namespace Granit.Authentication.JwtBearer.EntraId;

/// <summary>
/// Granit module for Entra ID extras (claims transformation, Admin policy).
/// Depends on <see cref="GranitAuthenticationJwtBearerModule"/> for generic JWT Bearer.
/// </summary>
[DependsOn(typeof(GranitAuthenticationJwtBearerModule))]
public sealed class GranitAuthenticationJwtBearerEntraIdModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntraId();
}
