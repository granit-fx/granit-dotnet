using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Tax.Domain;
using Granit.Tax.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Tax.EntityFrameworkCore.Internal;

/// <summary>Dedicated EF Core DbContext for Granit.Tax.</summary>
internal sealed class TaxDbContext(
    DbContextOptions<TaxDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<ValidatedTaxId> ValidatedTaxIds { get; set; } = null!;
    public DbSet<TaxRateOverride> TaxRateOverrides { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureTaxModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
        modelBuilder.ApplyEncryptionConventions(encryption);
    }
}
