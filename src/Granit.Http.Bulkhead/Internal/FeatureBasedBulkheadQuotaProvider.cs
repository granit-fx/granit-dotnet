using Granit.Features;
using Granit.Features.Exceptions;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Http.Bulkhead.Internal;

/// <summary>
/// Resolves bulkhead permit limits from <c>Granit.Features</c> Numeric features.
/// Convention: feature name = <c>Bulkhead.{PolicyName}</c> (overridable via <see cref="BulkheadPolicyOptions.FeatureName"/>).
/// Falls back to <see cref="OptionsBulkheadQuotaProvider"/> when the feature is not defined or
/// <c>IFeatureChecker</c> is not registered.
/// </summary>
internal sealed class FeatureBasedBulkheadQuotaProvider(
    IOptionsMonitor<GranitBulkheadOptions> options,
    ILogger<FeatureBasedBulkheadQuotaProvider> logger,
    IFeatureChecker? featureChecker = null) : IBulkheadQuotaProvider
{
    private volatile bool _warnedFeatureCheckerMissing;

    /// <inheritdoc/>
    public async Task<int?> GetPermitLimitAsync(string policyName, CancellationToken cancellationToken = default)
    {
        if (!options.CurrentValue.Policies.TryGetValue(policyName, out BulkheadPolicyOptions? policy))
        {
            return null;
        }

        if (featureChecker is not null)
        {
            string featureName = policy.FeatureName ?? $"Bulkhead.{policyName}";

            try
            {
                long value = await featureChecker.GetNumericAsync(featureName, cancellationToken).ConfigureAwait(false);
                if (value > 0)
                {
                    return (int)value;
                }
            }
            catch (FeatureNotFoundException)
            {
                // Feature not defined — fall through to static config.
            }
        }
        else if (!_warnedFeatureCheckerMissing)
        {
            _warnedFeatureCheckerMissing = true;
            BulkheadLog.LogFeatureCheckerMissing(logger);
        }

        return policy.PermitLimit;
    }
}
