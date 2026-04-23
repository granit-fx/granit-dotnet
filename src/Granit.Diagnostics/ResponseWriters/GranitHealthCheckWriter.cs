using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Diagnostics.ResponseWriters;

/// <summary>
/// Writes a structured JSON health check response for observability tooling (Grafana, Loki).
/// Kubernetes only reads the HTTP status code; the JSON payload is for operations teams.
/// </summary>
/// <remarks>
/// <para>
/// The response never contains stack traces, connection strings, tokens, or any PII,
/// in compliance with ISO 27001 and GDPR constraints.
/// </para>
/// <para>
/// Two writers are available:
/// <list type="bullet">
///   <item><see cref="WriteAsync"/> — full response including descriptions (for permission-gated endpoints).</item>
///   <item><see cref="WriteMinimalAsync"/> — strips descriptions to prevent information disclosure
///   on anonymous endpoints (Kubernetes probes).</item>
/// </list>
/// </para>
/// </remarks>
public static class GranitHealthCheckWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Writes the <see cref="HealthReport"/> as a JSON response, including descriptions.
    /// Intended for permission-gated monitoring endpoints.
    /// </summary>
    public static Task WriteAsync(HttpContext context, HealthReport report) =>
        WriteCore(context, report, includeDescriptions: true);

    /// <summary>
    /// Writes a minimal JSON health check response that omits descriptions.
    /// Intended for anonymous Kubernetes probe endpoints where descriptions could leak
    /// internal service state (ISO 27001 A.8.9, OWASP ASVS 14.3.3).
    /// </summary>
    public static Task WriteMinimalAsync(HttpContext context, HealthReport report) =>
        WriteCore(context, report, includeDescriptions: false);

    private static Task WriteCore(HttpContext context, HealthReport report, bool includeDescriptions)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        HealthResponse response = new(
            Status: report.Status.ToString(),
            Duration: Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            Checks: [.. report.Entries.Select(e => new CheckEntry(
                Name: e.Key,
                Status: e.Value.Status.ToString(),
                Duration: Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                Description: includeDescriptions ? e.Value.Description : null,
                Tags: [.. e.Value.Tags]))]);

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, SerializerOptions),
            context.RequestAborted);
    }

    private sealed record HealthResponse(
        string Status,
        double Duration,
        IReadOnlyList<CheckEntry> Checks);

    private sealed record CheckEntry(
        string Name,
        string Status,
        double Duration,
        string? Description,
        IReadOnlyList<string>? Tags);
}
