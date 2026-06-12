using Granit.Identity.UserSessions.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.UserSessions;

/// <summary>
/// Granit module making the identity provider the backend for the canonical user-session API:
/// registers <see cref="IdentityUserSessionProvider"/> and <see cref="IdentityUserDeviceProvider"/>
/// as the active providers (over <see cref="IIdentitySessionManager"/>), replacing the no-op defaults
/// so <c>/sessions</c> and <c>/devices</c> surface the IdP's SSO sessions and devices.
/// </summary>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitIdentityAbstractionsModule))]
public sealed class GranitIdentityUserSessionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.Replace(ServiceDescriptor.Scoped<IUserSessionProvider, IdentityUserSessionProvider>());
        context.Services.Replace(ServiceDescriptor.Scoped<IUserDeviceProvider, IdentityUserDeviceProvider>());
    }
}
