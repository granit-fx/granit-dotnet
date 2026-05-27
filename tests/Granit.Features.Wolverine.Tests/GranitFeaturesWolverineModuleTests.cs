using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Features.Wolverine.Tests;

public sealed class GranitFeaturesWolverineModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitFeaturesWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_IsGranitModule() => new GranitFeaturesWolverineModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnCore()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitFeaturesWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = [.. attrs.SelectMany(a => a.DependedTypes)];

        dependedTypes.ShouldContain(typeof(Granit.Features.GranitFeaturesModule));
    }
}
