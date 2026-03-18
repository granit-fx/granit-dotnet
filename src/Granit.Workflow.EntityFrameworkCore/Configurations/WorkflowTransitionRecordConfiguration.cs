using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Workflow.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WorkflowTransitionRecord"/>.
/// Table: <c>workflow_transition_records</c>.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: this table is INSERT-only. No UPDATE or DELETE operations
/// should ever be performed. Records must be retained for 3 years minimum.
/// </remarks>
internal sealed class WorkflowTransitionRecordConfiguration
    : IEntityTypeConfiguration<WorkflowTransitionRecord>
{
    public void Configure(EntityTypeBuilder<WorkflowTransitionRecord> builder)
    {
        builder.ToTable(
            GranitWorkflowDbProperties.DbTablePrefix + "transition_records",
            GranitWorkflowDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.EntityType)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.EntityId)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.PreviousState)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.NewState)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.TransitionedAt)
            .IsRequired();

        builder.Property(e => e.TransitionedBy)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(e => e.Comment)
            .HasMaxLength(2000);

        builder.Property(e => e.TenantId);

        // Hot path: query transition history for a specific entity
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.TransitionedAt })
            .HasDatabaseName($"ix_{GranitWorkflowDbProperties.DbTablePrefix}transition_records_entity_type_id_at");

        // RGPD: enables bulk export and erasure by tenant
        builder.HasIndex(e => new { e.TenantId, e.TransitionedAt })
            .HasDatabaseName($"ix_{GranitWorkflowDbProperties.DbTablePrefix}transition_records_tenantid_at");

        // Audit query: all transitions by a specific user
        builder.HasIndex(e => new { e.TransitionedBy, e.TransitionedAt })
            .HasDatabaseName($"ix_{GranitWorkflowDbProperties.DbTablePrefix}transition_records_by_user_at");
    }
}
