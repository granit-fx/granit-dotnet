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
/// Isolated EF Core <see cref="DbContext"/> for the canonical
/// <see cref="User"/> aggregate (ADR-051). Lives in <c>Granit.Identity</c>
/// (foundation) so every Granit app, even tiny ones without
/// <c>Granit.Parties</c>, gets the user table.
/// </summary>
/// <remarks>
/// Inherits from <see cref="GranitDbContext"/> so the multi-tenant filter is
/// parameterised (per-request) instead of inlined into the compiled SQL.
/// </remarks>
internal sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    private readonly IStringEncryptionService _encryption = encryption;

    /// <summary>The <see cref="User"/> table.</summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureGranitIdentityModule();
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
