using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationCycleRegistryTests
{
    private readonly MigrationCycleRegistry _registry = new();

    [Fact]
    public void Register_NewCycleId_StoresRegistration()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));

        _registry.Register("cycle-1", typeof(DbContext), migration);

        MigrationCycleRegistration? found = _registry.Find("cycle-1");
        found.ShouldNotBeNull();
        found!.CycleId.ShouldBe("cycle-1");
        found.DbContextType.ShouldBe(typeof(DbContext));
        found.Migration.ShouldBeSameAs(migration);
    }

    [Fact]
    public void Register_DuplicateCycleId_ThrowsInvalidOperationException()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));
        _registry.Register("cycle-dup", typeof(DbContext), migration);

        Action act = () => _registry.Register("cycle-dup", typeof(DbContext), migration);

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("cycle-dup");
    }

    [Fact]
    public void Register_IsCaseInsensitive()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));
        _registry.Register("CYCLE-CASE", typeof(DbContext), migration);

        Action act = () => _registry.Register("cycle-case", typeof(DbContext), migration);

        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void Find_UnknownCycleId_ReturnsNull()
    {
        MigrationCycleRegistration? result = _registry.Find("unknown");

        result.ShouldBeNull();
    }

    [Fact]
    public void Find_CaseInsensitive_ReturnsRegistration()
    {
        BatchMigrationDelegate migration = (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null));
        _registry.Register("CycleA", typeof(DbContext), migration);

        MigrationCycleRegistration? result = _registry.Find("cyclea");

        result.ShouldNotBeNull();
        result!.CycleId.ShouldBe("CycleA");
    }
}
