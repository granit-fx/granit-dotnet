using Granit.Auditing.Endpoints.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Auditing.Endpoints.Extensions;

/// <summary>
/// Extension methods for adding the Granit auditing health checks to <see cref="IHealthChecksBuilder"/>.
/// </summary>
public static class AuditingHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a health check that reports the depth of the async audit persistence channel,
    /// surfacing backpressure before the bounded channel saturates. Reports
    /// <see cref="HealthStatus.Healthy"/> when persistence runs in
    /// <see cref="Granit.Auditing.Domain.AuditPersistenceMode.Strict"/> (the channel is unused).
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The health check name. Defaults to <c>"auditing-channel"</c>.</param>
    /// <param name="failureStatus">
    /// The <see cref="HealthStatus"/> reported when the check fails. Defaults to
    /// <see cref="HealthStatus.Unhealthy"/>.
    /// </param>
    /// <param name="tags">Tags for filtering. Defaults to <c>["readiness"]</c>.</param>
    /// <param name="timeout">Optional probe timeout. Defaults to 10 seconds.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitAuditingChannelHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "auditing-channel",
        HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        tags ??= ["readiness"];
        return builder.AddCheck<AuditingChannelHealthCheck>(
            name, failureStatus, tags, timeout ?? TimeSpan.FromSeconds(10));
    }
}
