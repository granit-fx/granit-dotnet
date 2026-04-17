using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Payments.Internal;

/// <summary>
/// Default <see cref="IPaymentProviderResolver"/> that cross-references activated
/// <see cref="PaymentMethodConfiguration"/> records with registered
/// <see cref="IPaymentProvider"/> instances, and applies the availability filter
/// against the persisted capability snapshot on each record.
/// </summary>
/// <remarks>
/// Active configurations are cached in <see cref="IFusionCache"/> for 5 minutes.
/// The cache is proactively invalidated by the configuration endpoints on every
/// activate / deactivate / resync, so tenants see changes immediately.
///
/// <para>
/// Display labels are returned as the raw <c>MethodType</c> identifier. Callers
/// (typically the <c>GET /methods/available</c> endpoint) are responsible for
/// resolving a localized label via <c>IStringLocalizer</c>.
/// </para>
/// </remarks>
internal sealed class DefaultPaymentProviderResolver(
    IPaymentMethodConfigurationReader configReader,
    IEnumerable<IPaymentProvider> providers,
    IPaymentMethodAvailabilityFilter availabilityFilter,
    IFusionCache cache) : IPaymentProviderResolver
{
    private const string CacheKey = "granit:payments:active-configs";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public async Task<IPaymentProvider> ResolveAsync(Guid tenantId, string methodType,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs =
            await GetActiveConfigsCachedAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodConfiguration? config = configs.FirstOrDefault(
            c => c.MethodType.Equals(methodType, StringComparison.OrdinalIgnoreCase));

        if (config is null)
        {
            throw new InvalidOperationException(
                $"No active payment method configuration for method type '{methodType}'.");
        }

        IPaymentProvider? provider = providers.FirstOrDefault(
            p => p.Name.Equals(config.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            throw new InvalidOperationException(
                $"Payment provider '{config.ProviderName}' is configured for method type " +
                $"'{methodType}' but is not registered in DI.");
        }

        return provider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PaymentAvailableMethod>> GetAvailableProvidersAsync(
        Guid tenantId,
        PaymentAvailabilityContext? context = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs =
            await GetActiveConfigsCachedAsync(cancellationToken).ConfigureAwait(false);

        var providerByName = providers
            .ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

        List<PaymentAvailableMethod> result = [];

        foreach (PaymentMethodConfiguration config in configs)
        {
            if (!providerByName.TryGetValue(config.ProviderName, out IPaymentProvider? provider))
            {
                // Provider no longer installed — skip silently
                continue;
            }

            PaymentMethodDescriptor? descriptor = provider.SupportedMethods
                .FirstOrDefault(d => d.MethodType.Equals(config.MethodType, StringComparison.OrdinalIgnoreCase));

            if (descriptor is null)
            {
                // Provider no longer supports this method — skip silently
                continue;
            }

            PaymentMethodCapability? snapshot = config.GetCapabilitySnapshot();

            // Filter: when a context is supplied AND a snapshot exists, apply it.
            // No snapshot = legacy record, treated as wildcard (no filtering).
            if (context is not null && snapshot is not null
                && !availabilityFilter.IsAvailable(snapshot, context))
            {
                continue;
            }

            // Note: DisplayLabel is returned as the raw method type. The caller
            // (endpoint) localizes via IStringLocalizer.
            result.Add(new PaymentAvailableMethod(
                descriptor.MethodType,
                descriptor.Category,
                provider.Name,
                descriptor.MethodType,
                snapshot));
        }

        return result;
    }

    private Task<IReadOnlyList<PaymentMethodConfiguration>> GetActiveConfigsCachedAsync(
        CancellationToken cancellationToken) =>
        cache.GetOrSetAsync<IReadOnlyList<PaymentMethodConfiguration>>(
            CacheKey,
            async (_, ct) => await configReader.GetActiveAsync(ct).ConfigureAwait(false),
            new FusionCacheEntryOptions(CacheDuration),
            token: cancellationToken).AsTask();
}
