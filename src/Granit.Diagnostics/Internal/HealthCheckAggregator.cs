using System.Globalization;
using Granit.Diagnostics.Abstractions;
using Granit.Diagnostics.Dtos;
using Granit.Diagnostics.Options;
using Granit.Timing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.Diagnostics.Internal;

/// <summary>
/// Default implementation of <see cref="IHealthCheckAggregator"/> that executes all
/// registered health checks via <see cref="HealthCheckService"/> and caches the result
/// using the same double-check locking pattern as <see cref="Caching.CachedHealthCheck"/>.
/// </summary>
internal sealed class HealthCheckAggregator(
    HealthCheckService healthCheckService,
    IClock clock,
    IOptions<DiagnosticsOptions> options) : IHealthCheckAggregator, IDisposable
{
    private readonly HealthCheckService _healthCheckService = healthCheckService;
    private readonly IClock _clock = clock;
    private readonly TimeSpan _cacheDuration = options.Value.MonitoringCacheDuration;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private MonitoringHealthResponse? _cached;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    /// <inheritdoc/>
    public async Task<MonitoringHealthResponse> CheckAllAsync(CancellationToken cancellationToken = default)
    {
        // Fast path — no lock acquisition if cache is warm
        DateTimeOffset now = _clock.Now;
        if (_cached is not null && now < _expiresAt)
        {
            return _cached;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check after acquiring the lock
            now = _clock.Now;
            if (_cached is not null && now < _expiresAt)
            {
                return _cached;
            }

            HealthReport report = await _healthCheckService
                .CheckHealthAsync(cancellationToken)
                .ConfigureAwait(false);

            MonitoringHealthResponse response = MapReport(report, now);
            _cached = response;
            _expiresAt = now.Add(_cacheDuration);
            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();

    private static MonitoringHealthResponse MapReport(HealthReport report, DateTimeOffset checkedAt)
    {
        List<ServiceHealthResponse> services = new(report.Entries.Count);

        foreach (KeyValuePair<string, HealthReportEntry> entry in report.Entries)
        {
            services.Add(new ServiceHealthResponse(
                Id: entry.Key,
                Name: FormatDisplayName(entry.Key),
                Status: MapStatus(entry.Value.Status),
                ResponseTimeMs: Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                Description: entry.Value.Description,
                Tags: [.. entry.Value.Tags]));
        }

        return new MonitoringHealthResponse(services, checkedAt);
    }

    private static string MapStatus(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => "healthy",
        HealthStatus.Degraded => "degraded",
        _ => "down",
    };

    /// <summary>
    /// Converts a health check registration name (e.g. <c>"postgresql"</c>, <c>"blob-storage-s3"</c>)
    /// into a human-readable display name (<c>"PostgreSQL"</c>, <c>"Blob Storage S3"</c>).
    /// </summary>
    private static string FormatDisplayName(string registrationName)
    {
        Span<char> buffer = stackalloc char[registrationName.Length * 2];
        int pos = 0;
        bool capitalizeNext = true;

        foreach (char c in registrationName)
        {
            if (c is '-' or '_')
            {
                buffer[pos++] = ' ';
                capitalizeNext = true;
            }
            else
            {
                buffer[pos++] = capitalizeNext ? char.ToUpper(c, CultureInfo.InvariantCulture) : c;
                capitalizeNext = false;
            }
        }

        return new string(buffer[..pos]);
    }
}
