using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class GranitQueryEngineModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule()
    {
        GranitQueryEngineModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitQueryEngineModule).IsSealed.ShouldBeTrue();
}
