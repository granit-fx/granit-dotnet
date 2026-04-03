using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Payments.EntityFrameworkCore.Internal;

internal sealed class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.ToTable(GranitPaymentsDbProperties.DbTablePrefix + "processed_webhook_events", GranitPaymentsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ProviderName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.ProviderEventId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ProcessedAt).IsRequired();

        builder.HasIndex(e => new { e.ProviderName, e.ProviderEventId }).IsUnique()
            .HasDatabaseName($"uq_{GranitPaymentsDbProperties.DbTablePrefix}webhook_events_provider_event");
    }
}
