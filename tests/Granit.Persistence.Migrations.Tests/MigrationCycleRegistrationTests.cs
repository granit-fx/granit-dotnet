using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Migrations.Tests;

public sealed class MigrationCycleRegistrationTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        MigrationCycleRegistration registration = new("cycle-1", typeof(TestDbContext), migration);

        registration.CycleId.ShouldBe("cycle-1");
        registration.DbContextType.ShouldBe(typeof(TestDbContext));
        registration.Migration.ShouldBeSameAs(migration);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        MigrationCycleRegistration reg1 = new("cycle-1", typeof(TestDbContext), migration);
        MigrationCycleRegistration reg2 = new("cycle-1", typeof(TestDbContext), migration);

        reg1.ShouldBe(reg2);
    }

    [Fact]
    public void Equality_DifferentCycleId_AreNotEqual()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        MigrationCycleRegistration reg1 = new("cycle-1", typeof(TestDbContext), migration);
        MigrationCycleRegistration reg2 = new("cycle-2", typeof(TestDbContext), migration);

        reg1.ShouldNotBe(reg2);
    }
}
