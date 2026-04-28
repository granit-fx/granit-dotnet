using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy.Events;

namespace Granit.Parties.MultiTenancy.Handlers;

/// <summary>
/// Wolverine-discovered handler that seeds the host-scoped <c>Granit.Parties.Domain.Party</c>
/// representing every newly created tenant via <see cref="IDefaultPartySeeder"/>. Idempotent —
/// replays of the same <see cref="TenantCreatedEvent"/> return the existing party instead of
/// duplicating it. Runtime equivalent of the historical-data backfill the showcase / consuming
/// app would run as a one-shot migration script.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class SeedDefaultPartyOnTenantCreatedHandler
{
    public static Task HandleAsync(
        TenantCreatedEvent @event,
        IDefaultPartySeeder seeder,
        CancellationToken cancellationToken) =>
        seeder.SeedForTenantAsync(@event.TenantId, @event.Name, cancellationToken: cancellationToken);
}
