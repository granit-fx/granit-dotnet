using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Identity.EntityFrameworkCore.Internal.Configurations;

/// <summary>
/// EF Core configuration for <see cref="UserSessionReviewEntity"/>.
/// </summary>
internal sealed class UserSessionReviewEntityConfiguration : IEntityTypeConfiguration<UserSessionReviewEntity>
{
    public void Configure(EntityTypeBuilder<UserSessionReviewEntity> builder)
    {
        builder.ToTable(GranitIdentityDbProperties.DbTablePrefix + "user_session_reviews", GranitIdentityDbProperties.DbSchema);
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.UserId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.SessionId).HasMaxLength(128).IsRequired();
        builder.Property(e => e.Decision).HasMaxLength(16).IsRequired();
        builder.Property(e => e.ReviewedAt).IsRequired();

        // Single-use: at most one decision per (user, session). The unique constraint is the idempotency anchor.
        builder.HasIndex(e => new { e.UserId, e.SessionId }).IsUnique();
    }
}
