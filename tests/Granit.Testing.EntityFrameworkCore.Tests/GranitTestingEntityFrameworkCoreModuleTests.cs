using Granit.Modularity;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class GranitTestingEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_Inherits_GranitModule()
    {
        GranitTestingEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_Is_Sealed() => typeof(GranitTestingEntityFrameworkCoreModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_Has_DependsOn_PersistenceModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitTestingEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(Granit.Persistence.GranitPersistenceModule));
    }
}
