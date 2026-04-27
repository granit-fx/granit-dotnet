using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Parties.Domain;
using Granit.Parties.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore.Internal;

/// <summary>Dedicated EF Core <see cref="DbContext"/> for the central <see cref="Party"/> aggregate.</summary>
internal sealed class PartiesDbContext(
    DbContextOptions<PartiesDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Party> Parties { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureContactsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
