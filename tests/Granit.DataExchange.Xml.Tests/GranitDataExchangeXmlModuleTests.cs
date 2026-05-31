using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Xml.Tests;

public sealed class GranitDataExchangeXmlModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitDataExchangeXmlModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
