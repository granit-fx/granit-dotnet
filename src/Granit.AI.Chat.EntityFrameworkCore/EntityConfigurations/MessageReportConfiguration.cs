using Granit.AI.Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Chat.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="MessageReport"/> aggregate root.</summary>
internal sealed class MessageReportConfiguration : IEntityTypeConfiguration<MessageReport>
{
    public void Configure(EntityTypeBuilder<MessageReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIChatDbProperties.DbTablePrefix + "message_reports",
            GranitAIChatDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.MessageId).IsRequired();
        builder.Property(e => e.ConversationId).IsRequired();
        builder.Property(e => e.OwnerId).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(MessageReport.MaxReasonLength).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);

        // Category persists as its PascalCase string name via ApplyGranitConventions.

        // "Reports for a message" — review/moderation lookups, scoped within the tenant.
        builder.HasIndex(e => new { e.TenantId, e.MessageId })
            .HasDatabaseName($"ix_{GranitAIChatDbProperties.DbTablePrefix}message_reports_tenant_message");
    }
}
