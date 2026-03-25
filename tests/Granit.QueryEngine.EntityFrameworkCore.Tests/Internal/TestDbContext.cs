using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

internal sealed class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    {
    }

    public DbSet<TestProduct> Products => Set<TestProduct>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<TestProduct>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired();
        });
}
