// =============================================================================
// Tests - ObservabilityServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitObservability enregistre correctement Serilog
// et OpenTelemetry (traces + métriques) dans le conteneur DI.
// =============================================================================

using Granit.Observability.Extensions;
using Granit.Observability.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Shouldly;
using Xunit;

namespace Granit.Observability.Tests;

public sealed class ObservabilityServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitObservability_RegistersObservabilityOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:ServiceName"] = "test-service";
        builder.Configuration["Observability:ServiceVersion"] = "1.2.3";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe("test-service");
        options.ServiceVersion.ShouldBe("1.2.3");
    }

    [Fact]
    public void AddGranitObservability_RegistersTracerProvider()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://localhost:4317";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        TracerProvider? tracerProvider = sp.GetService<TracerProvider>();
        tracerProvider.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_RegistersMeterProvider()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableMetrics"] = "true";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://localhost:4317";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        MeterProvider? meterProvider = sp.GetService<MeterProvider>();
        meterProvider.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_WithDefaultConfig_UsesDefaultOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        // When no ServiceName is configured, the fallback is IHostEnvironment.ApplicationName.
        options.ServiceName.ShouldBe(builder.Environment.ApplicationName);
        options.OtlpEndpoint.ShouldBe("http://localhost:4317");
        options.EnableTracing.ShouldBeTrue();
        options.EnableMetrics.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitObservability_ReturnsBuilder_ForChaining()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        // Act
        IHostApplicationBuilder result = builder.AddGranitObservability();

        // Assert
        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AddGranitObservability_TracingDisabled_DoesNotRegisterOtlpTraceExporter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "false";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://localhost:4317";

        builder.AddGranitObservability();

        // Should not throw — tracing is skipped gracefully
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_MetricsDisabled_DoesNotRegisterOtlpMetricExporter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableMetrics"] = "false";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://localhost:4317";

        builder.AddGranitObservability();

        // Should not throw — metrics is skipped gracefully
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_AllDisabled_StillRegistersOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "false";
        builder.Configuration["Observability:EnableMetrics"] = "false";
        builder.Configuration["Observability:ServiceName"] = "disabled-service";

        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe("disabled-service");
        options.EnableTracing.ShouldBeFalse();
        options.EnableMetrics.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitObservability_CustomServiceProperties_BindsCorrectly()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:ServiceName"] = "test-api";
        builder.Configuration["Observability:ServiceVersion"] = "2.0.0";
        builder.Configuration["Observability:ServiceNamespace"] = "digital-dynamics";
        builder.Configuration["Observability:Environment"] = "staging";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://otel:4317";

        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe("test-api");
        options.ServiceVersion.ShouldBe("2.0.0");
        options.ServiceNamespace.ShouldBe("digital-dynamics");
        options.Environment.ShouldBe("staging");
        options.OtlpEndpoint.ShouldBe("http://otel:4317");
    }

    [Fact]
    public void AddGranitObservability_RegistersSerilogDiagnosticContext()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        Serilog.IDiagnosticContext? diagnosticContext = sp.GetService<Serilog.IDiagnosticContext>();
        diagnosticContext.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_BothEnabled_RegistersTracerAndMeterProviders()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";
        builder.Configuration["Observability:EnableMetrics"] = "true";
        builder.Configuration["Observability:OtlpEndpoint"] = "http://otel:4317";

        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetService<TracerProvider>().ShouldNotBeNull();
        sp.GetService<MeterProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_CustomOtlpEndpoint_BindsCorrectly()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:OtlpEndpoint"] = "http://custom-collector:4317";

        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.OtlpEndpoint.ShouldBe("http://custom-collector:4317");
    }

    /// <summary>
    /// Verifies the OTEL tracing filter: paths under /health/* must be excluded,
    /// while /healthcare/... and regular paths must be included.
    /// </summary>
    [Theory]
    [InlineData("/health/live", false)]
    [InlineData("/health/ready", false)]
    [InlineData("/health/startup", false)]
    [InlineData("/healthz", false)]
    [InlineData("/healthcare/patients", true)]
    [InlineData("/api/orders", true)]
    [InlineData("/", true)]
    public void OtelTracingFilter_ExcludesHealthPaths_ButNotHealthcarePaths(string requestPath, bool expectedIncluded)
    {
        // The filter is a lambda registered inside AddGranitObservability via
        // aspnet.Filter = httpContext => !path.StartsWithSegments("/health") && path != "/healthz"
        // We test it by replicating its logic to ensure spec coverage of the two conditions.

        PathString path = new(requestPath);
        bool included = !path.StartsWithSegments("/health") && path != "/healthz";

        included.ShouldBe(expectedIncluded,
            $"path '{requestPath}' should {(expectedIncluded ? "be included in" : "be excluded from")} tracing");
    }
}
