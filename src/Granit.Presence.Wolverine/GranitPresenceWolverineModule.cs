using Granit.Identity.Local;
using Granit.Modularity;
using Granit.Wolverine;

namespace Granit.Presence.Wolverine;

/// <summary>
/// Granit module wiring Wolverine handlers for <c>Granit.Presence</c>.
/// </summary>
/// <remarks>
/// <para>
/// When loaded alongside <see cref="GranitIdentityLocalModule"/>, it subscribes to
/// <see cref="Granit.Identity.Local.Events.AccountDeletedEto"/> and purges the
/// matching <c>UserPresence</c> row plus the cached heartbeat — the right-to-erasure
/// obligation for presence data (GDPR Art. 17).
/// </para>
/// <para>
/// Wolverine auto-discovers the handler from this assembly's exported types; the
/// module itself registers no services.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitIdentityLocalModule),
    typeof(GranitPresenceModule),
    typeof(GranitWolverineModule))]
public sealed class GranitPresenceWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
    }
}
