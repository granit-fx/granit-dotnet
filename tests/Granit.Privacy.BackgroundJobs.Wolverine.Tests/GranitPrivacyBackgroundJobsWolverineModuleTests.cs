using Granit.Modularity;
using Granit.Wolverine;
using Shouldly;
using Xunit;

namespace Granit.Privacy.BackgroundJobs.Wolverine.Tests;

public sealed class GranitPrivacyBackgroundJobsWolverineModuleTests
{
    [Fact]
    public void DependsOn_DeclaresPrivacyBackgroundJobsAndWolverineModules()
    {
        DependsOnAttribute? attribute = typeof(GranitPrivacyBackgroundJobsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute.DependedTypes.ShouldContain(typeof(GranitPrivacyBackgroundJobsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void Module_IsSealed_AndPublic()
    {
        // The framework module discovery scans public, instantiable Granit modules — a
        // private or abstract module would silently fail to load.
        typeof(GranitPrivacyBackgroundJobsWolverineModule).IsSealed.ShouldBeTrue();
        typeof(GranitPrivacyBackgroundJobsWolverineModule).IsPublic.ShouldBeTrue();
    }
}
