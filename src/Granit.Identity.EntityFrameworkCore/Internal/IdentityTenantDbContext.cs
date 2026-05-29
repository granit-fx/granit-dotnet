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
/// Tenant-isolated EF Core DbContext used under
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>. Holds
/// tenant users (<c>TenantId == &lt;tenant&gt;</c>). Each tenant's identity table lives
/// in its own schema (under <c>SchemaPerTenant</c>) or database (under
/// <c>DatabasePerTenant</c>) — <c>DROP SCHEMA &lt;tenant&gt; CASCADE</c> lessivage is the
/// GDPR Art. 17 right-of-erasure primitive enabled by this layout.
/// </summary>
/// <remarks>
/// Registered via <c>AddGranitIsolatedDbContext&lt;IdentityTenantDbContext&gt;</c>. The
/// companion host-side context is <see cref="IdentityHostDbContext"/>.
/// </remarks>
internal sealed class IdentityTenantDbContext(
    DbContextOptions<IdentityTenantDbContext> options,
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
