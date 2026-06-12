using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.UserSessions.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core configuration for <see cref="UserSessionRiskEntity"/>.
/// </summary>
internal sealed class UserSessionRiskEntityConfiguration : IEntityTypeConfiguration<UserSessionRiskEntity>
{
    public void Configure(EntityTypeBuilder<UserSessionRiskEntity> builder)
    {
        builder.ToTable(GranitUserSessionsDbProperties.DbTablePrefix + "risks", GranitUserSessionsDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.SessionId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Level).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ReasonsJson).IsRequired();
        builder.Property(e => e.AssessedAt).IsRequired();

        builder.HasIndex(e => new { e.UserId, e.SessionId }).IsUnique();
    }
}
