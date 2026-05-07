using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core <see cref="DbContext"/> for the canonical
/// <see cref="User"/> aggregate (ADR-051). Lives in <c>Granit.Identity</c>
/// (foundation) so every Granit app, even tiny ones without
/// <c>Granit.Parties</c>, gets the user table.
/// </summary>
/// <remarks>
/// Per the framework convention (CLAUDE.md, "Isolated DbContext —
/// MANDATORY"), this DbContext lazily injects <see cref="ICurrentTenant"/>
/// and <see cref="IDataFilter"/> and applies the framework conventions
/// (tenant + soft-delete + named query filters) via
/// <see cref="ModelBuilderExtensions.ApplyGranitConventions"/>.
/// </remarks>
internal sealed class IdentityDbContext(
    DbContextOptions<IdentityDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    private readonly IStringEncryptionService _encryption = encryption;
    private readonly ICurrentTenant? _currentTenant = currentTenant;
    private readonly IDataFilter? _dataFilter = dataFilter;

    /// <summary>The <see cref="User"/> table.</summary>
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        base.OnModelCreating(modelBuilder);

        modelBuilder.ConfigureGranitIdentityModule();
        modelBuilder.ApplyGranitConventions(_currentTenant, _dataFilter);
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
