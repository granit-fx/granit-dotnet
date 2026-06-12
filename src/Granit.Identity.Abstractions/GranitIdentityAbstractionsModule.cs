using Granit.Identity.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity;

/// <summary>
/// Granit module for shared session contracts (<see cref="UserSessionDescriptor"/>,
/// <see cref="IUserSessionProvider"/>, <see cref="IUserSessionAnomalyDetector"/>,
/// <see cref="IUserSessionRiskStore"/>).
/// </summary>
/// <remarks>
/// Registers safe no-op / in-memory defaults so the contracts resolve everywhere. Install a backend
/// integration package (BFF, OpenIddict, Keycloak) to replace the session/device providers,
/// <c>Granit.UserSessions.AnomalyDetection</c> to replace the detector, and
/// <c>Granit.UserSessions.EntityFrameworkCore</c> to replace the risk store with a durable one.
/// </remarks>
public sealed class GranitIdentityAbstractionsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IUserSessionAnomalyDetector, NullUserSessionAnomalyDetector>();
        context.Services.TryAddSingleton<IUserSessionRiskStore, MemoryUserSessionRiskStore>();
        context.Services.TryAddScoped<IUserSessionProvider, NullUserSessionProvider>();
        context.Services.TryAddScoped<IUserDeviceProvider, NullUserDeviceProvider>();
    }
}
