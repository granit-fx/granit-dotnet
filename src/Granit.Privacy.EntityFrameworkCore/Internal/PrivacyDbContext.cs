using Granit.DataFiltering;
using Granit.Encryption;
using Granit.Encryption.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Extensions;
using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

internal sealed class PrivacyDbContext(
    DbContextOptions<PrivacyDbContext> options,
    IStringEncryptionService encryption,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    private readonly IStringEncryptionService _encryption = encryption;

    public DbSet<LegalDocument> LegalDocuments { get; set; } = null!;

    public DbSet<ExportRequestEntity> ExportRequests { get; set; } = null!;

    public DbSet<ExportAssemblyCheckpointRow> ExportAssemblyCheckpoints { get; set; } = null!;

    public DbSet<DeletionRequestEntity> DeletionRequests { get; set; } = null!;

    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigurePrivacyModule();
        // Encrypts every string property carrying [Encrypted] (DeletionRequestEntity.Reason
        // for now). New entities inheriting LegalAgreementBase pick up encryption on
        // IpAddress automatically.
        modelBuilder.ApplyEncryptionConventions(_encryption);
    }
}
