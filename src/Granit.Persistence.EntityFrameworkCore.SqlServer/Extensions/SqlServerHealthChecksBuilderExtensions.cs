using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;

/// <summary>
/// Extension methods for adding a SQL Server health check to <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class SqlServerHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a lightweight SQL Server health check that opens a raw <see cref="SqlConnection"/>
    /// and executes <c>SELECT 1</c>.
    /// </summary>
    /// <remarks>
    /// Uses a direct <see cref="SqlConnection"/> (not EF Core) to avoid adding ORM overhead
    /// to the health-check path. The connection is opened and closed on every probe.
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="name">The health check name. Defaults to <c>"sqlserver"</c>.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> reported when the check fails.
    /// Defaults to <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Optional tags for filtering health checks.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitSqlServerHealthCheck(
        this IHealthChecksBuilder builder,
        string connectionString,
        string name = "sqlserver",
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null)
    {
        tags ??= ["readiness", "startup"];
        return builder.AddAsyncCheck(
            name,
            async ct =>
            {
                try
                {
                    await using SqlConnection connection = new(connectionString);
                    await connection.OpenAsync(ct).ConfigureAwait(false);
                    await using SqlCommand cmd = connection.CreateCommand();
                    cmd.CommandText = "SELECT 1";
                    await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    return HealthCheckResult.Healthy();
                }
                catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
                {
                    return new HealthCheckResult(failureStatus, description: "SQL Server health check was canceled.", exception: oce);
                }
                catch (Exception ex)
                {
                    return new HealthCheckResult(failureStatus, exception: ex);
                }
            },
            tags);
    }
}
