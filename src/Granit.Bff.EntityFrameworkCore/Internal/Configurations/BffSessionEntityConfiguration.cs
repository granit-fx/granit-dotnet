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

        builder.HasIndex(e => new { e.FrontendName, e.SessionId }).IsUnique();
        builder.HasIndex(e => new { e.FrontendName, e.UserId });
        builder.HasIndex(e => e.ExpiresAt); // For cleanup job
    }
}
