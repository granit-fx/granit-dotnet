using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Csv.Tests;

public sealed class GranitDataExchangeCsvModuleTests
{
    [Fact]
    public void Module_has_expected_dependency()
    {
        // Arrange
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeCsvModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .ToArray();

        // Assert
        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependedTypes.ShouldContain(typeof(GranitDataExchangeModule));
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitDataExchangeCsvModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_inherits_from_GranitModule() =>
        typeof(GranitDataExchangeCsvModule).BaseType.ShouldBe(typeof(GranitModule));
}
