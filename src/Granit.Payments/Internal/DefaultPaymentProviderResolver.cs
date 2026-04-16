using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace Granit.Payments.Internal;

/// <summary>
/// Default <see cref="IPaymentProviderResolver"/> that resolves available payment methods
/// from the <see cref="PaymentMethodConfiguration"/> store and cross-references with
/// registered <see cref="IPaymentProvider"/> instances in DI.
/// </summary>
/// <remarks>
/// Active configurations are cached in <see cref="IMemoryCache"/> for 5 minutes to avoid
/// hitting the database on every request. The cache expires automatically; no explicit
/// invalidation is needed for v1.
/// </remarks>
internal sealed class DefaultPaymentProviderResolver(
    IPaymentMethodConfigurationReader configReader,
    IEnumerable<IPaymentProvider> providers,
    IMemoryCache cache) : IPaymentProviderResolver
{
    private const string CacheKey = "granit:payments:active-configs";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public IPaymentProvider Resolve(Guid tenantId, string methodType)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs = GetActiveConfigsCached();

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
    public IReadOnlyList<PaymentAvailableMethod> GetAvailableProviders(Guid tenantId)
    {
        IReadOnlyList<PaymentMethodConfiguration> configs = GetActiveConfigsCached();

        HashSet<string> registeredProviders = new(
            providers.Select(p => p.Name),
            StringComparer.OrdinalIgnoreCase);

        return configs
            .Where(c => registeredProviders.Contains(c.ProviderName))
            .Select(c => new PaymentAvailableMethod(
                c.MethodType, c.Category, c.ProviderName, c.DisplayLabel))
            .ToList();
    }

    private IReadOnlyList<PaymentMethodConfiguration> GetActiveConfigsCached() =>
        cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            return configReader.GetActiveAsync().GetAwaiter().GetResult();
        })!;
}
