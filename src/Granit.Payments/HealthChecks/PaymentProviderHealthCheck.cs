using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Payments.HealthChecks;

/// <summary>
/// Readiness probe for a single <see cref="IPaymentProvider"/>. Calls
/// <see cref="IPaymentProvider.GetCatalogAsync"/> with a short timeout — a single
/// call covers authentication, reachability, and response parsing in one hit.
/// </summary>
/// <remarks>
/// <para>
/// Status mapping:
/// </para>
/// <list type="bullet">
///   <item><see cref="HealthCheckResult.Healthy"/> when the catalog returns at least one entry.</item>
///   <item><see cref="HealthCheckResult.Degraded"/> when the catalog is empty (auth OK, but nothing to offer).</item>
///   <item><see cref="HealthCheckResult.Unhealthy"/> on timeout, <see cref="UnauthorizedAccessException"/>, or any other exception.</item>
/// </list>
/// <para>
/// Security: error messages expose only the provider name and the exception type name —
/// never URLs, API keys, or tokens. Follows the same convention as
/// <c>HttpServiceHealthCheckBase</c>.
/// </para>
/// <para>
/// Built-in providers with a static catalog (SEPA Transfer, SEPA Direct Debit) are always
/// Healthy — registering the check for them is cheap and keeps the surface uniform.
/// </para>
/// </remarks>
public sealed class PaymentProviderHealthCheck(
    IPaymentProvider provider,
    TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(s_timeout, timeProvider);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            IReadOnlyList<Contracts.PaymentMethodCatalogEntry> catalog = await provider
                .GetCatalogAsync(linkedCts.Token)
                .ConfigureAwait(false);

            return catalog.Count == 0
                ? HealthCheckResult.Degraded($"{provider.Name} catalog empty")
                : HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{provider.Name} health check timed out");
        }
        catch (UnauthorizedAccessException)
        {
            return HealthCheckResult.Unhealthy($"{provider.Name} auth failed");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"{provider.Name} unreachable: {ex.GetType().Name}");
        }
    }
}
