using Granit.DataExchange;
using Granit.Localization;
using Granit.Modularity;
using Granit.MultiTenancy.Extensions;
using Granit.QueryEngine;

namespace Granit.MultiTenancy;

/// <summary>
/// Granit module for multi-tenant management.
/// Resolves the tenant from the HTTP header or the JWT claim (standard ClaimsPrincipal).
/// Compatible with any identity provider (Keycloak, Auth0, Azure AD, etc.).
/// </summary>
[DependsOn(typeof(GranitDataExchangeAbstractionsModule))]
[DependsOn(typeof(GranitLocalizationModule))]
[DependsOn(typeof(GranitQueryEngineModule))]
public sealed class GranitMultiTenancyModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitMultiTenancy();
}
