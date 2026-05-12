using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Analytics.Tests;

/// <summary>
/// Smoke tests for the Granit.BlobStorage.Analytics satellite module. The bulk of the
/// behavior is in pure declarative records (metric / dashboard definitions) and
/// is covered by the host framework module's analytics endpoints tests.
/// </summary>
public sealed class GranitBlobStorageAnalyticsModuleTests
{
    [Fact]
    public void Module_should_inherit_GranitModule()
    {
        typeof(GranitBlobStorageAnalyticsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_should_declare_DependsOn_attribute()
    {
        DependsOnAttribute? attr = typeof(GranitBlobStorageAnalyticsModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull("Satellite modules must declare their framework host and analytics-abstractions dependencies.");
        attr!.DependedTypes.ShouldNotBeEmpty();
    }
}
