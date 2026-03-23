using System.ComponentModel.DataAnnotations;

namespace Granit.Observability.Options;

/// <summary>
/// Configuration options for observability (logs, traces, metrics).
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Observability";

    /// <summary>Service name for OTEL (e.g. "my-backend").</summary>
    [Required]
    public string ServiceName { get; set; } = "unknown-service";

    /// <summary>Service version.</summary>
    [Required]
    public string ServiceVersion { get; set; } = "0.0.0";

    /// <summary>OTLP gRPC endpoint (e.g. http://otel-collector:4317).</summary>
    [Required]
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>Service namespace (e.g. "my-company").</summary>
    [Required]
    public string ServiceNamespace { get; set; } = "my-company";

    /// <summary>Deployment environment (e.g. "production", "staging", "development").</summary>
    [Required]
    public string Environment { get; set; } = "development";

    /// <summary>Enable trace export via OTLP. Default: true.</summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>Enable metrics export via OTLP. Default: true.</summary>
    public bool EnableMetrics { get; set; } = true;
}
