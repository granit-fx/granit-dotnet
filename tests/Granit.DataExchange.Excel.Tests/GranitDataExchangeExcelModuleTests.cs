using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Excel.Tests;

public sealed class GranitDataExchangeExcelModuleTests
{
    [Fact]
    public void Module_has_expected_dependency()
    {
        // Arrange
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeExcelModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .ToArray();

        // Assert
        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependedTypes.ShouldContain(typeof(GranitDataExchangeModule));
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitDataExchangeExcelModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_inherits_from_GranitModule() =>
        typeof(GranitDataExchangeExcelModule).BaseType.ShouldBe(typeof(GranitModule));
}
