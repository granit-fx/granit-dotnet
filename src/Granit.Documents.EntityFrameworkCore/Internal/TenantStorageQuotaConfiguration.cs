using Granit.Documents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="TenantStorageQuota"/>.
/// Table: <c>documents_tenant_storage_quotas</c>.
/// </summary>
internal sealed class TenantStorageQuotaConfiguration : IEntityTypeConfiguration<TenantStorageQuota>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TenantStorageQuota> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitDocumentsDbProperties.DbTablePrefix + "tenant_storage_quotas",
            GranitDocumentsDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.LimitBytes).IsRequired();
        builder.Property(e => e.UsageBytes).IsRequired();
        builder.Property(e => e.UpdatedAt).IsRequired();

        // Exactly one quota row per tenant. The unique index also lights up the
        // tenant-scoped GET path used by the bootstrap + service queries.
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName($"ux_{GranitDocumentsDbProperties.DbTablePrefix}tenant_storage_quotas_tenant");
    }
}
