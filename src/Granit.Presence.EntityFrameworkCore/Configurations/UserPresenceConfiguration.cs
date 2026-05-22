using Granit.Presence.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Presence.EntityFrameworkCore.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="UserPresence"/>.
/// Table: <c>presence_user_presence</c>.
/// </summary>
internal sealed class UserPresenceConfiguration : IEntityTypeConfiguration<UserPresence>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserPresence> builder)
    {
        builder.ToTable(
            GranitPresenceDbProperties.DbTablePrefix + "user_presence",
            GranitPresenceDbProperties.DbSchema);

        builder.HasKey(e => e.Id);

        // UserId is the natural identifier and is mirrored into Id by the factory.
        builder.Property(e => e.UserId)
            .IsRequired();

        builder.Property(e => e.ManualStatus)
            .IsRequired()
            .HasDefaultValue(ManualPresenceStatus.Available)
            .HasConversion<short>();

        builder.Property(e => e.OverrideUntilUtc);

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(450);

        builder.Property(e => e.ModifiedAt);

        builder.Property(e => e.ModifiedBy)
            .HasMaxLength(450);

        builder.HasIndex(e => e.UserId).IsUnique();
    }
}
