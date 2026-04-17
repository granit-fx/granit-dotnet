using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Granit.Payments.HealthChecks;

/// <summary>
/// Registers <see cref="PaymentProviderHealthCheck"/> for a single
/// <see cref="IPaymentProvider"/> identified by name.
/// </summary>
public static class PaymentProviderHealthCheckBuilderExtensions
{
    /// <summary>
    /// Adds a <see cref="PaymentProviderHealthCheck"/> named
    /// <c>payments-{providerName}</c>, tagged <c>readiness</c> and <c>payments</c>, with
    /// <see cref="HealthStatus.Degraded"/> as the failure status so a single misbehaving
    /// provider does not cascade a <c>/ready</c> outage.
    /// </summary>
    /// <param name="builder">The health-checks builder to register on.</param>
    /// <param name="providerName">
    /// Provider <see cref="IPaymentProvider.Name"/> — matched case-insensitively against the
    /// providers registered in DI.
    /// </param>
    public static IHealthChecksBuilder AddGranitPaymentProviderHealthCheck(
        this IHealthChecksBuilder builder, string providerName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return builder.Add(new HealthCheckRegistration(
            $"payments-{providerName}",
            sp => new PaymentProviderHealthCheck(
                ResolveProvider(sp, providerName),
                sp.GetRequiredService<TimeProvider>()),
            failureStatus: HealthStatus.Degraded,
            tags: ["readiness", "payments"]));
    }

    private static IPaymentProvider ResolveProvider(IServiceProvider sp, string providerName) =>
        sp.GetServices<IPaymentProvider>()
            .FirstOrDefault(p => p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"No IPaymentProvider named '{providerName}' is registered in DI.");
}
