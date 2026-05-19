using Granit.Guids;
using Granit.Modularity;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class GranitBackgroundJobsModuleTests
{
    [Fact]
    public void DependsOn_DeclaresGuidsSecurityAndTimingModules()
    {
        DependsOnAttribute? attribute = typeof(GranitBackgroundJobsModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitGuidsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitTimingModule));
    }

    [Fact]
    public void Module_IsGranitModule()
    {
        GranitBackgroundJobsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }
}
