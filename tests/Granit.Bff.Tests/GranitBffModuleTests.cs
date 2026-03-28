using Granit.Caching;
using Granit.Modularity;
using Granit.Oidc;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Bff.Tests;

public sealed class GranitBffModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitBffModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitBffModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_GranitCachingModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitCachingModule));
    }

    [Fact]
    public void Module_DependsOn_GranitOidcModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitOidcModule));
    }

    [Fact]
    public void Module_DependsOn_GranitTimingModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitTimingModule));
    }

    private static Type[] GetAllDependencies() =>
        typeof(GranitBffModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();
}
