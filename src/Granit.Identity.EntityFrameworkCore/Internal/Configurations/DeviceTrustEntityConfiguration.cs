using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Identity.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core configuration for <see cref="DeviceTrustEntity"/>.
/// </summary>
internal sealed class DeviceTrustEntityConfiguration : IEntityTypeConfiguration<DeviceTrustEntity>
{
    public void Configure(EntityTypeBuilder<DeviceTrustEntity> builder)
    {
        builder.ToTable(GranitIdentityDbProperties.DbTablePrefix + "device_trusts", GranitIdentityDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.DeviceId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Level).HasMaxLength(16).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(64);
        builder.Property(e => e.TrustedAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
    }
}
