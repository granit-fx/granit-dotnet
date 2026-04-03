using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Metering.EntityFrameworkCore.Tests;

public sealed class GranitMeteringEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitMeteringEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitMeteringEntityFrameworkCoreModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitMeteringModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMeteringEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitMeteringModule));
    }

    [Fact]
    public void Module_DependsOn_GranitPersistenceEntityFrameworkCoreModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitMeteringEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }
}
