using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Privacy.EntityFrameworkCore.DataDeletion;
using Granit.Privacy.EntityFrameworkCore.DataExport;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Extensions;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

internal sealed class PrivacyDbContext(
    DbContextOptions<PrivacyDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<LegalDocument> LegalDocuments { get; set; } = null!;

    public DbSet<ExportRequestEntity> ExportRequests { get; set; } = null!;

    public DbSet<DeletionRequestEntity> DeletionRequests { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigurePrivacyModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
        // Encrypts every string property carrying [Encrypted] (DeletionRequestEntity.Reason
        // for now). New entities inheriting LegalAgreementBase pick up encryption on
        // IpAddress automatically.
        modelBuilder.ApplyEncryptionConventions(encryption);
    }
}
