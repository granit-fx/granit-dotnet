using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Host-pinned EF Core DbContext that backs the canonical <see cref="User"/> aggregate
/// (ADR-051) for every user whose physical placement is the host schema.
/// </summary>
/// <remarks>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> this is
/// the single context that serves both host-admin and tenant users; the <c>MultiTenant</c>
/// row-level filter enforces per-tenant isolation.
/// </para>
/// <para>
/// Under <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/> this
/// context holds <i>only</i> host-admin users (<c>TenantId == null</c>) and tenant users
/// live in the companion <see cref="IdentityTenantDbContext"/>.
/// </para>
/// </remarks>
internal sealed class IdentityHostDbContext(
    DbContextOptions<IdentityHostDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), IIdentityDbContext
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <inheritdoc/>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureGranitIdentityModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
