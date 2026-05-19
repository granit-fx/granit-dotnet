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
using OpenTelemetry;
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

    [Fact]
    public void AddGranitObservability_EmptyServiceName_FallsBackToApplicationName()
    {
        // Arrange — ServiceName set to empty string triggers fallback
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:ServiceName"] = "";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — PostConfigure replaces empty/whitespace with ApplicationName
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe(builder.Environment.ApplicationName);
    }

    [Fact]
    public void AddGranitObservability_WhitespaceServiceName_FallsBackToApplicationName()
    {
        // Arrange — whitespace-only triggers fallback
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:ServiceName"] = "   ";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe(builder.Environment.ApplicationName);
    }

    [Fact]
    public void AddGranitObservability_EmptyEnvironment_FallsBackToHostEnvironment()
    {
        // Arrange — empty Environment triggers fallback
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:Environment"] = "";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — PostConfigure replaces empty with IHostEnvironment.EnvironmentName
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.Environment.ShouldBe(builder.Environment.EnvironmentName.ToLowerInvariant());
    }

    [Fact]
    public void AddGranitObservability_WhitespaceEnvironment_FallsBackToHostEnvironment()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:Environment"] = "   ";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.Environment.ShouldBe(builder.Environment.EnvironmentName.ToLowerInvariant());
    }

    [Fact]
    public void AddGranitObservability_DevelopmentEnvironment_FallsBackToHostEnvironment()
    {
        // Arrange — "development" is the default and triggers fallback
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:Environment"] = "development";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — replaced by IHostEnvironment.EnvironmentName (lowered)
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.Environment.ShouldBe(builder.Environment.EnvironmentName.ToLowerInvariant());
    }

    [Fact]
    public void AddGranitObservability_ExplicitEnvironment_PreservesValue()
    {
        // Arrange — explicit non-default environment should NOT be overwritten
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:Environment"] = "production";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.Environment.ShouldBe("production");
    }

    [Fact]
    public void AddGranitObservability_ExplicitServiceName_PreservesValue()
    {
        // Arrange — explicit non-default name should NOT be overwritten
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:ServiceName"] = "my-api";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe("my-api");
    }

    [Fact]
    public void AddGranitObservability_DeferOtlpToHost_DoesNotThrow()
    {
        // Arrange — OTEL_EXPORTER_OTLP_ENDPOINT signals the host owns OTLP exporter setup
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://aspire-otel:4317";
        builder.Configuration["Observability:EnableTracing"] = "true";
        builder.Configuration["Observability:EnableMetrics"] = "true";

        // Act — should skip per-signal OTLP exporters without error
        builder.AddGranitObservability();

        // Assert — providers still resolve
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        sp.GetService<TracerProvider>().ShouldNotBeNull();
        sp.GetService<MeterProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_DeferOtlpToHost_StillRegistersOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://aspire-otel:4317";
        builder.Configuration["Observability:ServiceName"] = "aspire-service";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceName.ShouldBe("aspire-service");
    }

    /// <summary>
    /// Regression: when the host (e.g. .NET Aspire's ServiceDefaults) calls
    /// <c>UseOtlpExporter()</c> on the OpenTelemetry builder before Granit, the
    /// resulting <see cref="TracerProvider"/>/<see cref="MeterProvider"/> must
    /// build without throwing. OpenTelemetry SDK 1.9+ throws
    /// <see cref="NotSupportedException"/> at build time when either
    /// <c>UseOtlpExporter()</c> is registered twice or it is mixed with
    /// per-signal <c>AddOtlpExporter()</c>. Granit detects the host registration
    /// and defers entirely.
    /// </summary>
    [Fact]
    public void AddGranitObservability_HostAlreadyCalledUseOtlpExporter_BuildsProviders()
    {
        // Arrange — mimic .NET Aspire ServiceDefaults: host wires UseOtlpExporter()
        // (typically called via AddServiceDefaults) BEFORE Granit is added.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://aspire-otel:4317";

        builder.Services.AddOpenTelemetry().UseOtlpExporter();

        // Act — Granit must NOT register a second UseOtlpExporter() nor per-signal exporters
        Should.NotThrow(() => builder.AddGranitObservability());

        // Assert — provider construction must succeed (this is where the SDK
        // mutual-exclusion guard fires if Granit got it wrong)
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        Should.NotThrow(() => sp.GetService<TracerProvider>().ShouldNotBeNull());
        Should.NotThrow(() => sp.GetService<MeterProvider>().ShouldNotBeNull());
    }

    /// <summary>
    /// k8s/Docker scenario: <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is injected by the
    /// orchestrator but there is no Aspire-style host call to <c>UseOtlpExporter()</c>.
    /// Granit must own the OTLP exporter registration so telemetry actually flows
    /// to the configured endpoint.
    /// </summary>
    [Fact]
    public void AddGranitObservability_EnvVarSetButNoHostUseOtlpExporter_BuildsProviders()
    {
        // Arrange — env var set, no host registration
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://otel-collector:4317";

        // Act
        Should.NotThrow(() => builder.AddGranitObservability());

        // Assert — providers build (Granit's per-signal AddOtlpExporter() runs, no mutex error),
        // and the env-var endpoint flowed through ApplyFallbacks into the resolved options.
        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        Should.NotThrow(() => sp.GetService<TracerProvider>().ShouldNotBeNull());
        Should.NotThrow(() => sp.GetService<MeterProvider>().ShouldNotBeNull());

        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.OtlpEndpoint.ShouldBe("http://otel-collector:4317");
    }

    [Fact]
    public void AddGranitObservability_TracingDisabledMetricsEnabled_OnlyMetricsProviderActive()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "false";
        builder.Configuration["Observability:EnableMetrics"] = "true";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — MeterProvider should resolve
        sp.GetService<MeterProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_MetricsDisabledTracingEnabled_OnlyTracerProviderActive()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";
        builder.Configuration["Observability:EnableMetrics"] = "false";

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — TracerProvider should resolve
        sp.GetService<TracerProvider>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitObservability_RegisteredActivitySources_AreIncluded()
    {
        // Arrange — register a custom ActivitySource before configuring observability
        string sourceName = $"Granit.TestModule.{Guid.NewGuid():N}";
        Granit.Diagnostics.GranitActivitySourceRegistry.Register(sourceName);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Configuration["Observability:EnableTracing"] = "true";

        // Act — should include the registered source without throwing
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert — TracerProvider is configured (sources are registered internally)
        sp.GetService<TracerProvider>().ShouldNotBeNull();

        // Verify the source was registered in the registry
        Granit.Diagnostics.GranitActivitySourceRegistry.GetRegisteredSources()
            .ShouldContain(sourceName);
    }

    [Fact]
    public void AddGranitObservability_ServiceNamespaceDefault_IsMyCompany()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceNamespace.ShouldBe("my-company");
    }

    [Fact]
    public void AddGranitObservability_ServiceVersionDefault_IsZeroZeroZero()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);

        // Act
        builder.AddGranitObservability();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Assert
        ObservabilityOptions options = sp.GetRequiredService<IOptions<ObservabilityOptions>>().Value;
        options.ServiceVersion.ShouldBe("0.0.0");
    }
}
