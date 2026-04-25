using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy.Events;
using Granit.Persistence.EntityFrameworkCore.Hosting;

namespace Granit.MultiTenancy.Provisioning.Handlers;

/// <summary>
/// Provisions a new tenant's database schema, runs EF Core migrations, and seeds
/// tenant-specific data when <see cref="TenantCreatedEvent"/> is dispatched at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Delegates all discovery and migration logic to <see cref="ITenantProvisioner"/>.
/// The default <c>AutoTenantProvisioner</c> discovers all tenant-isolated DbContexts
/// automatically — applications do not need to list them explicitly.
/// </para>
/// <para>
/// During <c>--migrate</c> seeding, Wolverine is not started and this handler is not
/// invoked. Cold-start provisioning is handled by <c>GranitMigrationRunner</c>'s
/// post-seed re-migration pass.
/// </para>
/// <para>
/// All operations are idempotent. Wolverine's retry policies can safely replay
/// this message without side effects.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class TenantProvisioningHandler
{
    /// <summary>
    /// Handles <see cref="TenantCreatedEvent"/> by provisioning the new tenant.
    /// </summary>
    public static async Task HandleAsync(
        TenantCreatedEvent evt,
        ITenantProvisioner provisioner,
        CancellationToken cancellationToken)
    {
        await provisioner.ProvisionAsync(evt.TenantId, evt.Name, cancellationToken)
            .ConfigureAwait(false);
    }
}
