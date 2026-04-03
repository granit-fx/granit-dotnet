using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Subscriptions.BackgroundJobs.Tests;

public sealed class GranitSubscriptionsBackgroundJobsModuleTests
{
    [Fact]
    public void Module_ShouldBeSealed()
    {
        typeof(GranitSubscriptionsBackgroundJobsModule).IsSealed.ShouldBeTrue();
    }

    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        typeof(GranitSubscriptionsBackgroundJobsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_CanBeInstantiated()
    {
        var module = new GranitSubscriptionsBackgroundJobsModule();
        module.ShouldNotBeNull();
    }
}
