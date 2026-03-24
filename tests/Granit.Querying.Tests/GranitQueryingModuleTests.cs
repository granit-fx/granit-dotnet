using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Querying.Tests;

public sealed class GranitQueryingModuleTests
{
    [Fact]
    public void Module_inherits_from_GranitModule()
    {
        GranitQueryingModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_is_sealed() =>
        typeof(GranitQueryingModule).IsSealed.ShouldBeTrue();
}
