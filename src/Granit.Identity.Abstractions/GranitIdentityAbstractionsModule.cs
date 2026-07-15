using Granit.Identity.Internal;
using Granit.Identity.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity;

/// <summary>
/// Granit module for the shared identity contracts: the session contracts
/// (<see cref="UserSessionDescriptor"/>, <see cref="IUserSessionProvider"/>,
/// <see cref="IUserSessionAnomalyDetector"/>, <see cref="IIdentitySecurityStateStore"/>) and the
/// single <see cref="IUserLookupHasher"/> shared by the local and federated PII stores.
/// </summary>
/// <remarks>
/// Registers safe no-op / in-memory defaults so the contracts resolve everywhere. Install a backend
/// integration package (BFF, OpenIddict, Keycloak) to replace the session/device providers,
/// <c>Granit.Identity.AnomalyDetection</c> to replace the detector, and
/// <c>Granit.Identity.EntityFrameworkCore</c> to replace the risk store with a durable one.
/// </remarks>
public sealed class GranitIdentityAbstractionsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IUserSessionAnomalyDetector, NullUserSessionAnomalyDetector>();
        context.Services.TryAddSingleton<IIdentitySecurityStateStore, MemoryIdentitySecurityStateStore>();
        context.Services.TryAddScoped<IUserSessionProvider, NullUserSessionProvider>();
        context.Services.TryAddScoped<IUserDeviceProvider, NullUserDeviceProvider>();

        // Single lookup hasher shared by the local (User) and federated (FederatedIdentity)
        // encrypted-PII stores. The pepper is bound from Identity:LookupHasher and validated
        // lazily in the hasher constructor (fail-fast when a store first needs it), so hosts
        // without at-rest encryption never require a pepper. Granit.Identity.Federated adds a
        // startup guard reconciling the legacy Identity:Federated:UserCacheHasher pepper.
        context.Services.AddOptions<UserLookupHasherOptions>()
            .BindConfiguration(UserLookupHasherOptions.SectionName);
        context.Services.TryAddSingleton<IUserLookupHasher, HmacUserLookupHasher>();
    }
}
