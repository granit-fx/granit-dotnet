using Granit.Persistence.EntityFrameworkCore.Migrations.Extensions;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationCycleRegistryExtensionsTests
{
    [Fact]
    public void Register_Generic_StoresCorrectDbContextType()
    {
        MigrationCycleRegistry registry = new();
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        IMigrationCycleRegistry returned = registry.Register<StubDbContext>("cycle-ext", migration);

        MigrationCycleRegistration? found = registry.Find("cycle-ext");
        found.ShouldNotBeNull();
        found!.DbContextType.ShouldBe(typeof(StubDbContext));
        returned.ShouldBeSameAs(registry);
    }

    [Fact]
    public void Register_Generic_ReturnsSameRegistry_ForFluentChaining()
    {
        MigrationCycleRegistry registry = new();
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        IMigrationCycleRegistry result = registry.Register<StubDbContext>("chain-cycle", migration);

        result.ShouldBeSameAs(registry);
    }

    private sealed class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options);
}
