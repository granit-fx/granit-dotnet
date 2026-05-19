using Granit.Events;
using Granit.Guids;
using Granit.Modularity;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Tests;

public sealed class GranitIdentityLocalModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitIdentityLocalModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitIdentityLocalModule).BaseType.ShouldBe(typeof(GranitModule));

    [Fact]
    public void Module_DependsOn_GranitEventsModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitEventsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitGuidsModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitGuidsModule));
    }

    [Fact]
    public void Module_DependsOn_GranitIdentityModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitIdentityModule));
    }

    [Fact]
    public void Module_DependsOn_GranitTimingModule()
    {
        Type[] dependencies = GetAllDependencies();
        dependencies.ShouldContain(typeof(GranitTimingModule));
    }

    private static Type[] GetAllDependencies() =>
        typeof(GranitIdentityLocalModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .Cast<DependsOnAttribute>()
            .SelectMany(a => a.DependedTypes)
            .ToArray();
}
