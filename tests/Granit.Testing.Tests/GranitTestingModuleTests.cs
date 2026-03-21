using Granit.Core.Modularity;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class GranitTestingModuleTests
{
    [Fact]
    public void Module_Inherits_GranitModule()
    {
        GranitTestingModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_Is_Sealed() => typeof(GranitTestingModule).IsSealed.ShouldBeTrue();
}
