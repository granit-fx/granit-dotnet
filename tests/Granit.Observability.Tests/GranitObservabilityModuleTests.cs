using Granit.Modularity;
using Granit.Observability.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace Granit.Observability.Tests;

public sealed class GranitObservabilityModuleTests
{
    [Fact]
    public void InheritsGranitModule() =>
        typeof(GranitObservabilityModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void IsSealed() =>
        typeof(GranitObservabilityModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void ConfigureServices_RegistersObservabilityOptions()
    {
        GranitObservabilityModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ShouldNotBeNull();
        options.ServiceName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ConfigureServices_RegistersTracerAndMeterProviders()
    {
        GranitObservabilityModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";
        builder.Configuration["Observability:EnableMetrics"] = "true";
        ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetService<TracerProvider>().ShouldNotBeNull();
        sp.GetService<MeterProvider>().ShouldNotBeNull();
    }
}
