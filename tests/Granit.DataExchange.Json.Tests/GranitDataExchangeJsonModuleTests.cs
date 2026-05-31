using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Json.Tests;

public sealed class GranitDataExchangeJsonModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitDataExchangeJsonModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
