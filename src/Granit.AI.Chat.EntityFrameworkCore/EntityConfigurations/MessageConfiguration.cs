using Granit.AI.Chat.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.AI.Chat.EntityFrameworkCore.EntityConfigurations;

/// <summary>EF Core configuration for the <see cref="Message"/> child entity.</summary>
internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GranitAIChatDbProperties.DbTablePrefix + "messages",
            GranitAIChatDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Content).IsRequired();
        builder.Property(e => e.WorkspaceKey).HasMaxLength(256);
        builder.Property(e => e.CreatedBy).HasMaxLength(256);

        // Role persists as its PascalCase string name via ApplyGranitConventions.

        builder.HasIndex(e => new { e.ConversationId, e.CreatedAt })
            .HasDatabaseName($"ix_{GranitAIChatDbProperties.DbTablePrefix}messages_conversation_created");
    }
}
