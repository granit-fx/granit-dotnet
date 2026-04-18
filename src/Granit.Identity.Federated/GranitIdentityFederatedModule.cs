using Granit.DataExchange.Extensions;
using Granit.Identity;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Exports;
using Granit.Identity.Federated.Queries;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Federated;

/// <summary>
/// Granit module providing federated identity abstractions.
/// Groups the shared dependency for all federated identity providers
/// (Keycloak, Entra ID, Cognito, Google Cloud).
/// </summary>
[DependsOn(typeof(GranitIdentityModule))]
public sealed class GranitIdentityFederatedModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Query + Export definitions (ADR-020: owned by the base module).
        context.Services.AddQueryDefinition<UserCacheEntry, UserCacheEntryQueryDefinition>();
        context.Services.AddExportDefinition<UserCacheEntry, UserCacheEntryExportDefinition>();
    }
}
