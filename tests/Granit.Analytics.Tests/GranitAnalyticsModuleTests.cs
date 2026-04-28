using Granit.Analytics.Diagnostics;
using Granit.Analytics.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests;

public sealed class GranitAnalyticsModuleTests
{
    [Fact]
    public void Module_IsGranitModule()
    {
        GranitAnalyticsModule module = new();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void AddGranitAnalytics_RegistersAnalyticsMetrics()
    {
        ServiceCollection services = new();
        services.AddMetrics();

        services.AddGranitAnalytics();

        AnalyticsMetrics resolved = services.BuildServiceProvider().GetRequiredService<AnalyticsMetrics>();
        resolved.ShouldNotBeNull();
    }
}
