// =============================================================================
// Tests - ObservabilityOptions
// =============================================================================
// Verifies that observability options have correct default values
// and that binding from configuration works correctly.
// =============================================================================

using Granit.Observability.Options;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Granit.Observability.Tests;

public sealed class ObservabilityOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new ObservabilityOptions();

        // Assert
        options.ServiceName.ShouldBe("unknown-service");
        options.ServiceVersion.ShouldBe("0.0.0");
        options.OtlpEndpoint.ShouldBe("http://localhost:4317");
        options.ServiceNamespace.ShouldBe("my-company");
        options.Environment.ShouldBe("development");
        options.EnableTracing.ShouldBeTrue();
        options.EnableMetrics.ShouldBeTrue();
    }

    [Fact]
    public void Binding_FromConfiguration_Works()
    {
        // Arrange
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObservabilityOptions.SectionName}:ServiceName"] = "test-backend",
                [$"{ObservabilityOptions.SectionName}:ServiceVersion"] = "1.0.0",
                [$"{ObservabilityOptions.SectionName}:OtlpEndpoint"] = "http://otel-collector:4317",
                [$"{ObservabilityOptions.SectionName}:Environment"] = "production",
                [$"{ObservabilityOptions.SectionName}:EnableTracing"] = "true",
                [$"{ObservabilityOptions.SectionName}:EnableMetrics"] = "false"
            })
            .Build();

        // Act
        ObservabilityOptions? options = config.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>();

        // Assert
        options.ShouldNotBeNull();
        options!.ServiceName.ShouldBe("test-backend");
        options.ServiceVersion.ShouldBe("1.0.0");
        options.OtlpEndpoint.ShouldBe("http://otel-collector:4317");
        options.Environment.ShouldBe("production");
        options.EnableTracing.ShouldBeTrue();
        options.EnableMetrics.ShouldBeFalse();
    }

    [Fact]
    public void SectionName_IsCorrect() => ObservabilityOptions.SectionName.ShouldBe("Observability");

    [Fact]
    public void SetServiceName_UpdatesValue()
    {
        ObservabilityOptions options = new() { ServiceName = "custom-api" };

        options.ServiceName.ShouldBe("custom-api");
    }

    [Fact]
    public void SetServiceVersion_UpdatesValue()
    {
        ObservabilityOptions options = new() { ServiceVersion = "3.1.0" };

        options.ServiceVersion.ShouldBe("3.1.0");
    }

    [Fact]
    public void SetOtlpEndpoint_UpdatesValue()
    {
        ObservabilityOptions options = new() { OtlpEndpoint = "http://remote:4317" };

        options.OtlpEndpoint.ShouldBe("http://remote:4317");
    }

    [Fact]
    public void SetServiceNamespace_UpdatesValue()
    {
        ObservabilityOptions options = new() { ServiceNamespace = "digital-dynamics" };

        options.ServiceNamespace.ShouldBe("digital-dynamics");
    }

    [Fact]
    public void SetEnvironment_UpdatesValue()
    {
        ObservabilityOptions options = new() { Environment = "staging" };

        options.Environment.ShouldBe("staging");
    }

    [Fact]
    public void SetEnableTracing_ToFalse_UpdatesValue()
    {
        ObservabilityOptions options = new() { EnableTracing = false };

        options.EnableTracing.ShouldBeFalse();
    }

    [Fact]
    public void SetEnableMetrics_ToFalse_UpdatesValue()
    {
        ObservabilityOptions options = new() { EnableMetrics = false };

        options.EnableMetrics.ShouldBeFalse();
    }

    [Fact]
    public void Binding_PartialConfiguration_KeepsDefaults()
    {
        // Arrange — only set ServiceName, leave everything else at defaults
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ObservabilityOptions.SectionName}:ServiceName"] = "partial-service"
            })
            .Build();

        // Act
        ObservabilityOptions? options = config.GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>();

        // Assert
        options.ShouldNotBeNull();
        options!.ServiceName.ShouldBe("partial-service");
        options.ServiceVersion.ShouldBe("0.0.0");
        options.OtlpEndpoint.ShouldBe("http://localhost:4317");
        options.ServiceNamespace.ShouldBe("my-company");
        options.Environment.ShouldBe("development");
        options.EnableTracing.ShouldBeTrue();
        options.EnableMetrics.ShouldBeTrue();
    }

    [Fact]
    public void Binding_EmptySection_ReturnsDefaults()
    {
        // Arrange — section exists but is empty
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        ObservabilityOptions options = new();
        config.GetSection(ObservabilityOptions.SectionName).Bind(options);

        // Assert — all defaults preserved
        options.ServiceName.ShouldBe("unknown-service");
        options.ServiceVersion.ShouldBe("0.0.0");
        options.Environment.ShouldBe("development");
    }
}
