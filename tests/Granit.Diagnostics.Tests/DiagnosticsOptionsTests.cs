using Granit.Diagnostics.Options;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class DiagnosticsOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        DiagnosticsOptions options = new();

        options.LivenessPath.ShouldBe("/health/live");
        options.ReadinessPath.ShouldBe("/health/ready");
        options.StartupPath.ShouldBe("/health/startup");
        options.DefaultCacheDuration.ShouldBe(TimeSpan.FromSeconds(10));
        options.MonitoringCacheDuration.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void LivenessPath_CanBeCustomized()
    {
        DiagnosticsOptions options = new() { LivenessPath = "/ping" };

        options.LivenessPath.ShouldBe("/ping");
    }

    [Fact]
    public void ReadinessPath_CanBeCustomized()
    {
        DiagnosticsOptions options = new() { ReadinessPath = "/ready" };

        options.ReadinessPath.ShouldBe("/ready");
    }

    [Fact]
    public void StartupPath_CanBeCustomized()
    {
        DiagnosticsOptions options = new() { StartupPath = "/started" };

        options.StartupPath.ShouldBe("/started");
    }

    [Fact]
    public void DefaultCacheDuration_CanBeCustomized()
    {
        DiagnosticsOptions options = new() { DefaultCacheDuration = TimeSpan.FromMinutes(1) };

        options.DefaultCacheDuration.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void MonitoringCacheDuration_CanBeCustomized()
    {
        DiagnosticsOptions options = new() { MonitoringCacheDuration = TimeSpan.FromMinutes(5) };

        options.MonitoringCacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
