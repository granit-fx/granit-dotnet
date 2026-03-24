using Granit.Authentication.EntraId.Extensions;
using Granit.Authentication.JwtBearer;
using Granit.Modularity;

namespace Granit.Authentication.EntraId;

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
