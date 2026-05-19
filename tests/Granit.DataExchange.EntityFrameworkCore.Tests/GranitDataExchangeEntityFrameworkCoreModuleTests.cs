using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.EntityFrameworkCore.Tests;

public sealed class GranitDataExchangeEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_has_expected_dependency()
    {
        // Arrange
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .ToArray();

        // Assert
        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependedTypes.ShouldContain(typeof(GranitDataExchangeModule));
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitDataExchangeEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_inherits_from_GranitModule() =>
        typeof(GranitDataExchangeEntityFrameworkCoreModule).BaseType.ShouldBe(typeof(GranitModule));
}
