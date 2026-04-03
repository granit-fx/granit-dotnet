using Granit.Features.Plans;
using Granit.Subscriptions.Domain.ValueObjects;

namespace Granit.Subscriptions.Features.Internal;

/// <summary>
/// Reads plan-level feature values from the Plan aggregate for the Features cascade.
/// </summary>
internal sealed class PlanFeatureValueStore(
    IPlanReader planReader) : IPlanFeatureStore
{
    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(
        string planId,
        string featureName,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(planId, out Guid guid))
        {
            return null;
        }

        Domain.Plan? plan = await planReader
            .GetByIdAsync(PlanId.Create(guid), cancellationToken)
            .ConfigureAwait(false);

        return plan?.PlanFeatureValues
            .FirstOrDefault(f => string.Equals(f.FeatureName, featureName, StringComparison.Ordinal))
            ?.Value;
    }
}
