namespace Granit.Features.Plans;

/// <summary>
/// Persistence contract for feature values assigned at the plan level.
/// </summary>
/// <remarks>
/// Implement and register this interface to enable plan-based feature overrides.
/// The built-in providers return <c>null</c> unless this interface is registered;
/// plan-level values then take precedence over defaults but yield to tenant overrides.
/// <para>
/// Registration:
/// <c>services.AddScoped&lt;IPlanFeatureStore, YourPlanFeatureStore&gt;();</c>
/// </para>
/// </remarks>
public interface IPlanFeatureStore
{
    /// <summary>
    /// Returns the feature value for the given <paramref name="planId"/> and
    /// <paramref name="featureName"/>, or <c>null</c> if the plan does not override
    /// the default value.
    /// </summary>
    Task<string?> GetOrNullAsync(string planId, string featureName, CancellationToken cancellationToken = default);
}
