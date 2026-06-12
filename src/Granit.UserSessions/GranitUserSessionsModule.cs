using Granit.Modularity;
using Granit.UserSessions.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.UserSessions;

/// <summary>
/// Granit module providing the user-session orchestrator (<see cref="IUserSessionManager"/>) — the single,
/// topology-agnostic entry point for listing and revoking a user's sessions and devices.
/// </summary>
/// <remarks>
/// The orchestrator is backed by the no-op session/device providers from
/// <c>Granit.Identity.Abstractions</c> until a backend integration package (BFF, OpenIddict, Keycloak)
/// registers a real <see cref="IUserSessionProvider"/>. Map the HTTP surface with
/// <c>Granit.UserSessions.Endpoints</c>.
/// </remarks>
[DependsOn(typeof(GranitIdentityAbstractionsModule))]
public sealed class GranitUserSessionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IUserSessionManager, DefaultUserSessionManager>();
}
