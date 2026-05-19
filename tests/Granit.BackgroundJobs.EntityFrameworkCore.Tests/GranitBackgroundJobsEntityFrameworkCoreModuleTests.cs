using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

public sealed class GranitBackgroundJobsEntityFrameworkCoreModuleTests
{
    [Fact]
    public void DependsOn_DeclaresBackgroundJobsAndPersistenceModules()
    {
        DependsOnAttribute? attribute = typeof(GranitBackgroundJobsEntityFrameworkCoreModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitBackgroundJobsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreModule));
    }

    [Fact]
    public void Module_IsGranitModule()
    {
        GranitBackgroundJobsEntityFrameworkCoreModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
