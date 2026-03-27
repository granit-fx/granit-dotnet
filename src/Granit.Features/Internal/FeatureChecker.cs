using Granit.Features.Cache;
using Granit.Features.Definitions;
using Granit.Features.Diagnostics;
using Granit.Features.Exceptions;
using Granit.Features.ValueProviders;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Features.Internal;

/// <summary>
/// Resolves feature values using the Tenant → Plan → Default cascade,
/// backed by <see cref="IFusionCache"/> (L1 in-process + L2 Redis + backplane).
/// </summary>
internal sealed class FeatureChecker(
    IFeatureDefinitionStore definitionStore,
    IEnumerable<IFeatureValueProvider> valueProviders,
    IServiceProvider serviceProvider,
    IFusionCache cache,
    FeaturesMetrics metrics) : IFeatureChecker
{
    private readonly IFeatureDefinitionStore _definitionStore = definitionStore;
    private readonly IReadOnlyList<IFeatureValueProvider> _providers =
        [.. valueProviders.OrderBy(p => p.Order)];
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IFusionCache _cache = cache;
    private readonly FeaturesMetrics _metrics = metrics;

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        string value = await GetValueAsync(featureName, cancellationToken).ConfigureAwait(false);
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public async Task<long> GetNumericAsync(string featureName, CancellationToken cancellationToken = default)
    {
        string value = await GetValueAsync(featureName, cancellationToken).ConfigureAwait(false);
        return long.TryParse(value, out long parsed) ? parsed : 0L;
    }

    /// <inheritdoc/>
    public async Task<string> GetValueAsync(string featureName, CancellationToken cancellationToken = default)
    {
        FeatureDefinition definition = _definitionStore.GetRequired(featureName);
        ICurrentTenant? currentTenant = _serviceProvider.GetService<ICurrentTenant>();
        Guid? tenantId = currentTenant?.IsAvailable == true ? currentTenant.Id : null;
        string cacheKey = FeatureCacheKey.Build(tenantId, featureName);
        string? tenantIdStr = tenantId?.ToString();

        string resolved = await _cache.GetOrSetAsync<string>(
            cacheKey,
            async (_, ct) =>
            {
                (string? value, string providerName) = await ResolveAsync(definition, ct).ConfigureAwait(false);
                _metrics.RecordValueResolved(tenantIdStr, featureName, providerName);
                return value ?? definition.DefaultValue;
            },
            token: cancellationToken).ConfigureAwait(false);

        return resolved;
    }

    /// <inheritdoc/>
    public async Task RequireEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        bool enabled = await IsEnabledAsync(featureName, cancellationToken).ConfigureAwait(false);
        if (!enabled)
        {
            throw new FeatureNotEnabledException(featureName);
        }
    }

    private async Task<(string? Value, string ProviderName)> ResolveAsync(
        FeatureDefinition definition,
        CancellationToken cancellationToken)
    {
        foreach (IFeatureValueProvider provider in _providers)
        {
            string? value = await provider.GetOrNullAsync(definition, cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                return (value, provider.Name);
            }
        }

        return (null, "Default");
    }
}
