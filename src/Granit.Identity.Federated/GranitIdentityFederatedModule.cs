using Granit.DataExchange.Extensions;
using Granit.Identity;
using Granit.Identity.Federated.Domain;
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
        context.Services.AddQueryDefinition<UserCacheEntry, UserCacheEntryQueryDefinition>();
        context.Services.AddExportDefinition<UserCacheEntry, UserCacheEntryExportDefinition>();

        // Default to a no-op rate limiter on token-exchange. Hosts that wire
        // Granit.RateLimiting can replace this registration with a distributed-store
        // implementation to enforce per-user quotas across pods.
        context.Services.TryAddSingleton<ITokenExchangeRateLimiter, NullTokenExchangeRateLimiter>();

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
