using Granit.OpenIddict.EntityFrameworkCore.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Health-check registration extensions for OpenIddict EF Core persistence.
/// </summary>
public static class OpenIddictHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a readiness health check for the OpenIddict signing-key store and its
    /// <see cref="Internal.OpenIddictDbContext"/>, tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// </summary>
    /// <remarks>
    /// Uncached — consistent with the framework's own <c>AddGranitDbContextHealthCheck</c>; the
    /// check runs a single lightweight <c>EXISTS</c> query. The check opens a scope per probe, so it
    /// is registered as a singleton without capturing the scoped DbContext factory.
    /// </remarks>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"openiddict-signing-keys"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitOpenIddictHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "openiddict-signing-keys",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<OpenIddictSigningKeyHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<OpenIddictSigningKeyHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
