using Granit.Privacy.LegalAgreements.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Privacy.EntityFrameworkCore.Internal;

internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable(
            GranitPrivacyDbProperties.DbTablePrefix + "legal_documents",
            GranitPrivacyDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.DocumentId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.DisplayName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.TemplateName)
            .HasMaxLength(500);

        builder.Property(e => e.LifecycleStatus)
            .HasMaxLength(20)
            .IsRequired();

        // At most one Published version per DocumentId (per tenant).
        // The IsPublished flag is synced by WorkflowTransitionInterceptor.
        builder.HasIndex(e => new { e.TenantId, e.DocumentId })
            .IsUnique()
            .HasFilter("\"IsPublished\" = true")
            .HasDatabaseName($"uq_{GranitPrivacyDbProperties.DbTablePrefix}legal_documents_published");

        // Version history lookup.
        builder.HasIndex(e => new { e.DocumentId, e.Version })
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}legal_documents_version");

        // Tenant-scoped queries.
        builder.HasIndex(e => new { e.TenantId, e.DocumentId, e.LifecycleStatus })
            .HasDatabaseName($"ix_{GranitPrivacyDbProperties.DbTablePrefix}legal_documents_tenant_status");
    }
}
