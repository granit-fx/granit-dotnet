using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Identity.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core configuration for <see cref="UserBehavioralProfileEntity"/>.
/// </summary>
internal sealed class UserBehavioralProfileEntityConfiguration : IEntityTypeConfiguration<UserBehavioralProfileEntity>
{
    public void Configure(EntityTypeBuilder<UserBehavioralProfileEntity> builder)
    {
        builder.ToTable(GranitIdentityDbProperties.DbTablePrefix + "user_behavioral_profiles", GranitIdentityDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Kind).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Value).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Count).IsRequired();
        builder.Property(e => e.FirstSeenAt).IsRequired();
        builder.Property(e => e.LastSeenAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.Kind, e.Value }).IsUnique();
    }
}
