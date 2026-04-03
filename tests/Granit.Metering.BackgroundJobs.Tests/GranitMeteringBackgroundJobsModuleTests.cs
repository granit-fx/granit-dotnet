using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Metering.BackgroundJobs.Tests;

public sealed class GranitMeteringBackgroundJobsModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitMeteringBackgroundJobsModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        typeof(GranitMeteringBackgroundJobsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitMeteringBackgroundJobsModule();
        module.ShouldNotBeNull();
    }
}
