using Granit.Observability.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace Granit.Observability.Tests;

public sealed class GranitOpenTelemetryRegistryTests
{
    [Fact]
    public void RegisterTracing_ContributorIsAppliedDuringAddGranitObservability()
    {
        // Arrange — register a sentinel ActivitySource via a tracing contributor.
        string sourceName = $"Granit.Test.Tracing.{Guid.NewGuid():N}";
        bool invoked = false;
        GranitOpenTelemetryRegistry.RegisterTracing(tracing =>
        {
            invoked = true;
            tracing.AddSource(sourceName);
        });

        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";

        // Act
        builder.AddGranitObservability();
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetService<TracerProvider>().ShouldNotBeNull();

        // Assert
        invoked.ShouldBeTrue("the registered tracing contributor must be applied to the TracerProviderBuilder");
    }

    [Fact]
    public void RegisterMetrics_ContributorIsAppliedDuringAddGranitObservability()
    {
        // Arrange
        bool invoked = false;
        GranitOpenTelemetryRegistry.RegisterMetrics(_ => invoked = true);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableMetrics"] = "true";

        // Act
        builder.AddGranitObservability();
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetService<MeterProvider>().ShouldNotBeNull();

        // Assert
        invoked.ShouldBeTrue("the registered metrics contributor must be applied to the MeterProviderBuilder");
    }

    [Fact]
    public void RegisterTracing_NullConfigure_Throws() =>
        Should.Throw<ArgumentNullException>(() => GranitOpenTelemetryRegistry.RegisterTracing(null!));

    [Fact]
    public void RegisterMetrics_NullConfigure_Throws() =>
        Should.Throw<ArgumentNullException>(() => GranitOpenTelemetryRegistry.RegisterMetrics(null!));
}
