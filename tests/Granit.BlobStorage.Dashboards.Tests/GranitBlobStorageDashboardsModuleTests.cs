using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Dashboards.Tests;

/// <summary>
/// Smoke tests for the Granit.BlobStorage.Dashboards satellite module. The bulk of the
/// behavior is in pure declarative records (metric / dashboard definitions) and
/// is covered by the host framework module's analytics endpoints tests.
/// </summary>
public sealed class GranitBlobStorageDashboardsModuleTests
{
    [Fact]
    public void Module_should_inherit_GranitModule()
    {
        typeof(GranitBlobStorageDashboardsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_should_declare_DependsOn_attribute()
    {
        DependsOnAttribute? attr = typeof(GranitBlobStorageDashboardsModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull("Satellite modules must declare their framework host and analytics-abstractions dependencies.");
        attr!.DependedModules.ShouldNotBeEmpty();
    }
}
