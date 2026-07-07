using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Abstractions.Tests;

public sealed class GranitQueryEngineAbstractionsModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule()
    {
        GranitQueryEngineAbstractionsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitQueryEngineAbstractionsModule).IsSealed.ShouldBeTrue();
}
