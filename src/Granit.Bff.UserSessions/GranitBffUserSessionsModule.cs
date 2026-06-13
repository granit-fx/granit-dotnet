using Granit.Bff.UserSessions.Internal;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Modularity;

namespace Granit.Bff.UserSessions;

/// <summary>
/// Granit module making the BFF the backend for the canonical user-session API: registers
/// <see cref="BffUserSessionProvider"/> as the active <see cref="IUserSessionProvider"/>, replacing the
/// no-op default so <c>/sessions</c> surfaces and revokes the user's browser↔BFF sessions.
/// </summary>
[DependsOn(
    typeof(GranitBffModule),
    typeof(GranitIdentityAbstractionsModule))]
public sealed class GranitBffUserSessionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.SetUserSessionProvider<BffUserSessionProvider>(UserSessionProviderPrecedence.Bff);
}
