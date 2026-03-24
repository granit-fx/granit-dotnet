using Granit.Modularity;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Hosting.Tests;

public class MigratableModuleDiscoveryTests
{
    [Fact]
    public void Should_detect_IMigratableModule_via_reflection()
    {
        // Arrange
        GranitModule migratableModule = new TestMigratableModule();
        GranitModule nonMigratableModule = new TestNonMigratableModule();

        // Act
        Type? migratable = GetMigratableDbContextType(migratableModule);
        Type? nonMigratable = GetMigratableDbContextType(nonMigratableModule);

        // Assert
        migratable.ShouldBe(typeof(TestDbContext));
        nonMigratable.ShouldBeNull();
    }

    [Fact]
    public void Should_extract_correct_DbContext_type_parameter()
    {
        GranitModule module = new TestMigratableModule();

        Type? dbContextType = GetMigratableDbContextType(module);

        dbContextType.ShouldNotBeNull();
        dbContextType.ShouldBe(typeof(TestDbContext));
        typeof(DbContext).IsAssignableFrom(dbContextType).ShouldBeTrue();
    }

    private static Type? GetMigratableDbContextType(GranitModule module)
    {
        Type moduleType = module.GetType();
        Type? migratableInterface = moduleType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType &&
                                 i.GetGenericTypeDefinition() == typeof(IMigratableModule<>));

        return migratableInterface?.GetGenericArguments()[0];
    }

    // --- Test fixtures ---

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options);

    private sealed class TestNonMigratableModule : GranitModule;

    private sealed class TestMigratableModule : GranitModule, IMigratableModule<TestDbContext>;
}
