using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Tests.Infrastructure;

/// <summary>
/// Application DbContext for tests, containing <see cref="TestEntity"/>.
/// </summary>
internal sealed class TestAppDbContext(DbContextOptions<TestAppDbContext> options)
    : DbContext(options)
{
    public DbSet<TestEntity> TestEntities { get; set; } = null!;

    // A unique constraint the executor's poison-row-isolation tests can violate on purpose.
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TestEntity>().HasIndex(e => e.Niss).IsUnique();
}
