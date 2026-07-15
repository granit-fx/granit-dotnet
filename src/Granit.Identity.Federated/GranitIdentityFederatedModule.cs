using Granit.DataExchange.Extensions;
using Granit.Entities.Extensions;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.Entities;
using Granit.Identity.Federated.Exports;
using Granit.Identity.Federated.Options;
using Granit.Identity.Federated.Queries;
using Granit.Identity.Federated.RateLimiting;
using Granit.Identity.Options;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.Configuration;
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

        // The lookup hasher and its Identity:LookupHasher pepper are now owned by
        // GranitIdentityAbstractionsModule — one hasher shared by the local User store and
        // the federated cache so a given email produces the same EmailHash on both sides.
        // Reconcile the legacy Federated-specific pepper here (Vague 2b):
        //   (a) adopt a legacy pepper when the unified key is unset, so existing federated
        //       deployments keep resolving their persisted EmailHash digests;
        //   (b) fail fast if BOTH keys are set to DIFFERENT values — that would silently
        //       break federated lookups, and a re-hash migration is required first.
        const string legacyPepperKey = "Identity:Federated:UserCacheHasher:EmailLookupPepper";
        const string unifiedPepperKey = UserLookupHasherOptions.SectionName + ":Pepper";

        context.Services.AddOptions<UserLookupHasherOptions>()
            .PostConfigure<IConfiguration>((opts, config) =>
            {
                string? legacy = config[legacyPepperKey];
                if (string.IsNullOrWhiteSpace(opts.Pepper) && !string.IsNullOrWhiteSpace(legacy))
                {
                    opts.Pepper = legacy;
                }
            })
            .Validate<IConfiguration>(
                (_, config) =>
                {
                    string? legacy = config[legacyPepperKey];
                    string? unified = config[unifiedPepperKey];
                    return string.IsNullOrWhiteSpace(legacy)
                        || string.IsNullOrWhiteSpace(unified)
                        || string.Equals(legacy, unified, StringComparison.Ordinal);
                },
                $"'{legacyPepperKey}' and '{unifiedPepperKey}' are set to different values. The " +
                "federated user cache and the local user directory now share one lookup pepper. Keep " +
                "only Identity:LookupHasher:Pepper, and its value MUST reproduce the persisted EmailHash " +
                "digests. If the peppers genuinely differ, re-hash the federated EmailHash column under " +
                "the unified pepper before removing the legacy key.")
            .Validate(
                opts => !string.IsNullOrWhiteSpace(opts.Pepper),
                $"{unifiedPepperKey} is required when Granit.Identity.Federated is loaded. Generate 32 " +
                "bytes of entropy (openssl rand -hex 32) and store it in Vault or an equivalent secret " +
                "store — NEVER bake it into source-controlled appsettings.json.")
            .ValidateOnStart();
    }
}
