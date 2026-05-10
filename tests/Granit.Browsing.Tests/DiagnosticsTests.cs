using Granit.Browsing.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void ActivitySource_should_carry_canonical_name() =>
        BrowsingActivitySource.Name.ShouldBe("Granit.Browsing");

    [Fact]
    public void Metrics_meter_name_should_match_module_convention() =>
        BrowsingMetrics.MeterName.ShouldBe("Granit.Browsing");

    [Fact]
    public void Metrics_should_resolve_via_DI()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddSingleton<BrowsingMetrics>();

        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<BrowsingMetrics>().ShouldNotBeNull();
    }
}
