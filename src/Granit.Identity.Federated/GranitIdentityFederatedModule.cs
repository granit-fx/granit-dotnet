using Granit.DataExchange.Extensions;
using Granit.Entities.Extensions;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Entities;
using Granit.Identity.Federated.Exports;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.Identity.Federated.Queries;
using Granit.Identity.Federated.RateLimiting;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        context.Services.AddQueryDefinition<FederatedIdentity, FederatedIdentityQueryDefinition>();
        context.Services.AddExportDefinition<FederatedIdentity, FederatedIdentityExportDefinition>();

        // Phase 2 EntityDefinition (ADR-050).
        context.Services.AddEntityDefinition<FederatedIdentity, FederatedIdentityEntityDefinition>();

        // Default to a no-op rate limiter on token-exchange. Hosts that wire
        // Granit.RateLimiting can replace this registration with a distributed-store
        // implementation to enforce per-user quotas across pods.
        context.Services.TryAddSingleton<ITokenExchangeRateLimiter, NullTokenExchangeRateLimiter>();

        // Throttles IdentityUserSyncFailedEto emissions per (UserId, ProviderName)
        // to one per cool-off window (default 60 min) so a sync-loop incident does
        // not flood the SIEM / notification channel. In-memory by default — multi-pod
        // hosts should swap in a Granit.RateLimiting-backed implementation.
        context.Services.AddOptions<IdentityFederatedNotificationOptions>()
            .BindConfiguration(IdentityFederatedNotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        context.Services.TryAddSingleton<IUserSyncFailureRateLimiter, InMemoryUserSyncFailureRateLimiter>();

        // Lookup hasher for email-based admin search over encrypted PII.
        // Startup-validated via UserCacheHasherOptions.EmailLookupPepper.
        context.Services.AddOptions<UserCacheHasherOptions>()
            .BindConfiguration(UserCacheHasherOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.EmailLookupPepper),
                $"{UserCacheHasherOptions.SectionName}:EmailLookupPepper is required when " +
                "Granit.Identity.Federated is loaded. Generate 32 bytes of entropy " +
                "(openssl rand -hex 32) and store it in Vault or an equivalent secret store — " +
                "NEVER bake it into source-controlled appsettings.json.")
            .ValidateOnStart();
        context.Services.TryAddSingleton<IUserLookupHasher, HmacUserLookupHasher>();
    }
}
