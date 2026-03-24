using Granit.Modularity;
using Granit.Timing;
using Granit.Validation;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests;

public sealed class GranitDataExchangeModuleTests
{
    [Fact]
    public void Module_has_expected_dependencies()
    {
        // Arrange
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .ToArray();

        // Assert
        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependedTypes.ShouldContain(typeof(GranitTimingModule));
        dependedTypes.ShouldContain(typeof(GranitValidationModule));
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitDataExchangeModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_inherits_from_GranitModule() =>
        typeof(GranitDataExchangeModule).BaseType.ShouldBe(typeof(GranitModule));
}
