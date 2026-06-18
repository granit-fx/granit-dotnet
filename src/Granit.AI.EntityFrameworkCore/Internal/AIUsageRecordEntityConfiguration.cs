using Granit.AI.EntityFrameworkCore.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core Fluent API configuration for <see cref="AIUsageRecordEntity"/>.
/// Table: <c>ai_usage_records</c>.
/// </summary>
internal sealed class AIUsageRecordEntityConfiguration : IEntityTypeConfiguration<AIUsageRecordEntity>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AIUsageRecordEntity> builder)
    {
        builder.ToTable(
            GranitAIDbProperties.DbTablePrefix + "usage_records",
            GranitAIDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId);

        builder.Property(e => e.UserId);

        builder.Property(e => e.WorkspaceName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.Provider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Model)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.InputTokens)
            .IsRequired();

        builder.Property(e => e.OutputTokens)
            .IsRequired();

        // Precision 18,8 for accurate cost tracking (e.g. 0.00000150 per token).
        builder.Property(e => e.EstimatedCost)
            .HasPrecision(18, 8);

        builder.Property(e => e.CostCurrency)
            .HasMaxLength(3);

        builder.Property(e => e.Duration);

        builder.Property(e => e.ConversationId);

        builder.Property(e => e.PromptVersion)
            .HasMaxLength(50);

        builder.Property(e => e.PromptTemplateName)
            .HasMaxLength(200);

        builder.Property(e => e.PromptTemplateVersion);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256)
            .IsRequired();

        // Tenant-scoped queries by workspace and time range (billing, dashboards).
        builder.HasIndex(e => new { e.TenantId, e.WorkspaceName, e.CreatedAt })
            .HasDatabaseName($"ix_{GranitAIDbProperties.DbTablePrefix}usage_records_tenant_workspace_date");

        // Cost aggregation queries by provider/model.
        builder.HasIndex(e => new { e.TenantId, e.Provider, e.Model })
            .HasDatabaseName($"ix_{GranitAIDbProperties.DbTablePrefix}usage_records_tenant_provider_model");

        // Per-conversation usage slicing (billing, audit). Sparse: null for non-chat interactions.
        builder.HasIndex(e => new { e.TenantId, e.ConversationId })
            .HasDatabaseName($"ix_{GranitAIDbProperties.DbTablePrefix}usage_records_tenant_conversation");
    }
}
