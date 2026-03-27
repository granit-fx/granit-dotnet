using Granit.Features.Diagnostics;
using Granit.Features.Exceptions;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Features.Internal;

/// <summary>
/// Enforces numeric feature limits using the resolved value from <see cref="IFeatureChecker"/>.
/// </summary>
internal sealed class FeatureLimitGuard(
    IFeatureChecker featureChecker,
    IServiceProvider serviceProvider,
    FeaturesMetrics metrics,
    ILogger<FeatureLimitGuard> logger) : IFeatureLimitGuard
{
    private readonly IFeatureChecker _featureChecker = featureChecker;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly FeaturesMetrics _metrics = metrics;
    private readonly ILogger<FeatureLimitGuard> _logger = logger;

    /// <inheritdoc/>
    public async Task CheckAsync(string featureName, long currentCount, CancellationToken cancellationToken = default)
    {
        long limit = await _featureChecker.GetNumericAsync(featureName, cancellationToken).ConfigureAwait(false);
        string? tenantId = ResolveTenantId();

        _metrics.RecordLimitChecked(tenantId, featureName);

        if (currentCount >= limit)
        {
            _metrics.RecordLimitExceeded(tenantId, featureName);
            FeaturesLog.FeatureLimitExceeded(_logger, featureName, tenantId ?? "global", currentCount, limit);
            throw new FeatureLimitExceededException(featureName, currentCount, limit);
        }
    }

    /// <inheritdoc/>
    public Task<long> GetLimitAsync(string featureName, CancellationToken cancellationToken = default) =>
        _featureChecker.GetNumericAsync(featureName, cancellationToken);

    private string? ResolveTenantId()
    {
        ICurrentTenant? currentTenant = _serviceProvider.GetService<ICurrentTenant>();
        return currentTenant?.IsAvailable == true ? currentTenant.Id?.ToString() : null;
    }
}
