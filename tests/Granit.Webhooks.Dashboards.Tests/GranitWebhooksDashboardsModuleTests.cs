using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Dashboards.Tests;

/// <summary>
/// Smoke tests for the Granit.Webhooks.Dashboards satellite module. The bulk of the
/// behavior is in pure declarative records (metric / dashboard definitions) and
/// is covered by the host framework module's analytics endpoints tests.
/// </summary>
public sealed class GranitWebhooksDashboardsModuleTests
{
    [Fact]
    public void Module_should_inherit_GranitModule()
    {
        typeof(GranitWebhooksDashboardsModule).IsSubclassOf(typeof(GranitModule)).ShouldBeTrue();
    }

    [Fact]
    public void Module_should_declare_DependsOn_attribute()
    {
        DependsOnAttribute? attr = typeof(GranitWebhooksDashboardsModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull("Satellite modules must declare their framework host and analytics-abstractions dependencies.");
        attr!.DependedTypes.ShouldNotBeEmpty();
    }
}
