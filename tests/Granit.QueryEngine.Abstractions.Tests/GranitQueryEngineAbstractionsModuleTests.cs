using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class GranitQueryEngineAbstractionsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitQueryEngineAbstractionsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
