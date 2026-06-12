using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Bff.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core configuration for <see cref="BffSessionEntity"/>.
/// </summary>
internal sealed class BffSessionEntityConfiguration : IEntityTypeConfiguration<BffSessionEntity>
{
    public void Configure(EntityTypeBuilder<BffSessionEntity> builder)
    {
        builder.ToTable(GranitBffDbProperties.DbTablePrefix + "sessions", GranitBffDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.SessionId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FrontendName).HasMaxLength(64).IsRequired();
        builder.Property(e => e.UserId).HasMaxLength(128);
        builder.Property(e => e.SerializedTokens).IsRequired();
        builder.Property(e => e.ExpiresAt).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.LastAccessedAt);

        // Holds an encrypted IP (ciphertext is longer than the 45-char raw IPv6 maximum).
        builder.Property(e => e.IpAddress).HasMaxLength(256);

        builder.HasIndex(e => new { e.FrontendName, e.SessionId }).IsUnique();
        builder.HasIndex(e => new { e.FrontendName, e.UserId });
        builder.HasIndex(e => e.ExpiresAt); // For cleanup job
    }
}
