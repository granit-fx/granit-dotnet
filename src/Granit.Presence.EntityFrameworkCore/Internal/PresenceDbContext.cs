using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Presence.Domain;
using Granit.Presence.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Presence.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core DbContext for the Granit.Presence persistence layer.
/// </summary>
/// <remarks>
/// Inherits <see cref="GranitDbContext"/> for the canonical conventions (audited interceptors,
/// soft-delete, named query filters) even though <see cref="UserPresence"/> is not
/// <c>IMultiTenant</c> — the tenant filter ends up a no-op for this context and the
/// architecture suite enforces the canonical inheritance.
/// </remarks>
internal sealed class PresenceDbContext(
    DbContextOptions<PresenceDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>User presence overrides.</summary>
    public DbSet<UserPresence> UserPresences => Set<UserPresence>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyGranitConventions();
        modelBuilder.ApplyConfiguration(new UserPresenceConfiguration());
    }
}
