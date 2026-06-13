using Granit.AI.Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Chat.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="Conversation"/> aggregate root.</summary>
internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIChatDbProperties.DbTablePrefix + "conversations",
            GranitAIChatDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title).HasMaxLength(500).IsRequired();
        builder.Property(e => e.OwnerId).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256);
        builder.Property(e => e.ModifiedBy).HasMaxLength(256);
        builder.Property(e => e.DeletedBy).HasMaxLength(256);

        // "My conversations, newest first" — owner-scoped listing within the tenant.
        builder.HasIndex(e => new { e.TenantId, e.OwnerId, e.CreatedAt })
            .HasDatabaseName($"ix_{GranitAIChatDbProperties.DbTablePrefix}conversations_tenant_owner_created");

        builder.HasMany(e => e.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
