using Granit.Bff.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Bff.EntityFrameworkCore;

/// <summary>
/// Isolated DbContext for BFF session persistence. Stores encrypted token sets
/// as an alternative to <see cref="IDistributedCache"/>-backed storage.
/// </summary>
public sealed class BffDbContext(DbContextOptions<BffDbContext> options) : DbContext(options)
{
    /// <summary>BFF sessions.</summary>
    public DbSet<BffSessionEntity> Sessions => Set<BffSessionEntity>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BffSessionEntity>(b =>
        {
            b.ToTable("bff_sessions");
            b.HasKey(e => e.Id);
            b.Property(e => e.Id).ValueGeneratedNever();
            b.Property(e => e.SessionId).HasMaxLength(64).IsRequired();
            b.Property(e => e.FrontendName).HasMaxLength(64).IsRequired();
            b.Property(e => e.UserId).HasMaxLength(128);
            b.Property(e => e.SerializedTokens).IsRequired();
            b.Property(e => e.ExpiresAt).IsRequired();
            b.Property(e => e.CreatedAt).IsRequired();

            b.HasIndex(e => new { e.FrontendName, e.SessionId }).IsUnique();
            b.HasIndex(e => new { e.FrontendName, e.UserId });
            b.HasIndex(e => e.ExpiresAt); // For cleanup job
        });
    }
}
