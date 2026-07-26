using Granit.OpenIddict.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.HealthChecks;

/// <summary>
/// Readiness health check for the OpenIddict signing-key store and its backing
/// <see cref="OpenIddictDbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// A single query against the signing-key table exercises both the DbContext connection and the
/// schema, so this one check reports DbContext readiness and signing-key availability together:
/// </para>
/// <list type="bullet">
///   <item>Store unreachable (database down, schema not migrated) → <see cref="HealthStatus.Unhealthy"/>.</item>
///   <item>Key rotation enabled but no active key present → <see cref="HealthStatus.Unhealthy"/>;
///   the server cannot mint tokens until a key is seeded.</item>
///   <item>Otherwise → <see cref="HealthStatus.Healthy"/>. With rotation disabled an empty table is
///   healthy — signing credentials then come from ephemeral or configured keys, not the store.</item>
/// </list>
/// <para>
/// Registered as a singleton; opens a scope per probe to resolve the scoped
/// <see cref="IDbContextFactory{TContext}"/>, so there is no captive dependency.
/// </para>
/// <para>The response never surfaces key material, connection strings, or tenant identifiers.</para>
/// </remarks>
internal sealed class OpenIddictSigningKeyHealthCheck(
    IServiceScopeFactory scopeFactory,
    IOptions<GranitKeyRotationOptions> rotationOptions) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IDbContextFactory<OpenIddictDbContext> dbContextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();

            await using OpenIddictDbContext db = await dbContextFactory
                .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            // Touching the signing-key table proves both connectivity and schema presence.
            bool hasActiveKey = await db.SigningKeys
                .AsNoTracking()
                .AnyAsync(k => k.Status == SigningKeyStatus.Active, cancellationToken)
                .ConfigureAwait(false);

            if (rotationOptions.Value.Enabled && !hasActiveKey)
            {
                return HealthCheckResult.Unhealthy(
                    "Key rotation is enabled but no active OpenIddict signing key is present.");
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Sanitize: never surface connection strings or key material in the probe response.
            return HealthCheckResult.Unhealthy(
                $"OpenIddict signing-key store unreachable: {ex.GetType().Name}");
        }
    }
}
