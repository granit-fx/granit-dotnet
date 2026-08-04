using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;

/// <summary>
/// Extension methods for adding a PostgreSQL health check to <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class NpgsqlHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a lightweight PostgreSQL health check that opens a raw <see cref="NpgsqlConnection"/>
    /// and executes <c>SELECT 1</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a direct <see cref="NpgsqlConnection"/> (not EF Core) to avoid adding ORM overhead
    /// to the health-check path. The connection is opened and closed on every probe.
    /// </para>
    /// <para>
    /// The probe is bounded by <paramref name="timeout"/> (default 10 seconds) so a hung TCP
    /// connect cannot block the readiness endpoint for the driver's default connect timeout.
    /// Failure results carry a sanitized description only — the raw driver exception (which
    /// leaks host/port/database to any verbose health writer) is never attached (ISO 27001).
    /// </para>
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <param name="name">The health check name. Defaults to <c>"postgres"</c>.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> reported when the check fails.
    /// Defaults to <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags for filtering health checks.</param>
    /// <param name="timeout">Per-probe ceiling. Defaults to 10 seconds.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitPostgresHealthCheck(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "postgres",
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        tags ??= ["readiness", "startup"];
        TimeSpan probeTimeout = timeout ?? TimeSpan.FromSeconds(10);
        return builder.AddAsyncCheck(
            name,
            async ct =>
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(probeTimeout);
                try
                {
                    await using NpgsqlConnection connection = new(connectionString);
                    await connection.OpenAsync(timeoutCts.Token).ConfigureAwait(false);
                    await using NpgsqlCommand cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    await cmd.ExecuteScalarAsync(timeoutCts.Token).ConfigureAwait(false);
                    return HealthCheckResult.Healthy();
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    return new HealthCheckResult(
                        failureStatus,
                        description: $"PostgreSQL health check timed out after {probeTimeout.TotalSeconds:0}s.");
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return new HealthCheckResult(failureStatus, description: "PostgreSQL health check was canceled.");
                }
                catch (Exception ex)
                {
                    // Sanitized on purpose: the raw exception message can contain host, port,
                    // database, and user — do not attach it to the probe result.
                    return new HealthCheckResult(
                        failureStatus,
                        description: $"PostgreSQL connection failed ({ex.GetType().Name}).");
                }
            },
            tags);
    }
}
