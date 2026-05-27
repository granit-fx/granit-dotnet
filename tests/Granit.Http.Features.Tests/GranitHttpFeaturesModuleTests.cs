using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Http.Features.Tests;

public sealed class GranitHttpFeaturesModuleTests
{
    [Fact]
    public void Module_IsSealed() => typeof(GranitHttpFeaturesModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_IsGranitModule() => new GranitHttpFeaturesModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnCore()
    {
        var attrs = (DependsOnAttribute[])typeof(GranitHttpFeaturesModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false);

        Type[] dependedTypes = [.. attrs.SelectMany(a => a.DependedTypes)];

        dependedTypes.ShouldContain(typeof(Granit.Features.GranitFeaturesModule));
    }
}
