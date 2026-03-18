using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AIWorkspaceEntity"/>.
/// Table: <c>ai_workspaces</c>.
/// </summary>
internal sealed class AIWorkspaceEntityConfiguration : IEntityTypeConfiguration<AIWorkspaceEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AIWorkspaceEntity> builder)
    {
        builder.ToTable(
            GranitAIDbProperties.DbTablePrefix + "workspaces",
            GranitAIDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.SystemPrompt)
            .HasMaxLength(10000);

        builder.Property(e => e.Temperature);

        builder.Property(e => e.MaxOutputTokens);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedAt);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.ModifiedAt);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(256);

        // Workspace name is unique per tenant.
        builder.HasIndex(e => new { e.TenantId, e.Name })
            .IsUnique()
            .HasDatabaseName($"uq_{GranitAIDbProperties.DbTablePrefix}workspaces_tenant_name");
    }
}
