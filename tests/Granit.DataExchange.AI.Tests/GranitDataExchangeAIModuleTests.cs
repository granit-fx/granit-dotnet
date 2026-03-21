using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.AI.Tests;

public sealed class GranitDataExchangeAIModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitDataExchangeAIModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitDataExchangeAIModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_HasExpectedDependencies()
    {
        DependsOnAttribute[] attributes = typeof(GranitDataExchangeAIModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .ToArray();

        Type[] dependedTypes = attributes.SelectMany(a => a.DependedTypes).ToArray();
        dependedTypes.ShouldContain(typeof(GranitDataExchangeModule));
    }
}
