using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Abstractions.Tests;

public sealed class GranitDataExchangeAbstractionsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitDataExchangeAbstractionsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
