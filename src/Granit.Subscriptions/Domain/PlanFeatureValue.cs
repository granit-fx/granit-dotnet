using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// Maps a feature name to a value within a plan.
/// Feeds into <c>Granit.Features</c> cascade via <c>IPlanFeatureStore</c>.
/// </summary>
public sealed class PlanFeatureValue : Entity
{
    private PlanFeatureValue() { }

    /// <summary>Creates a new plan feature value mapping.</summary>
    public static PlanFeatureValue Create(Guid id, string featureName, string value) =>
        new()
        {
            Id = id,
            FeatureName = featureName,
            Value = value,
        };

    /// <summary>
    /// The feature name as declared in <c>IFeatureDefinitionProvider</c>
    /// (e.g., <c>"Acme.MaxUsers"</c>).
    /// </summary>
    public string FeatureName { get; private set; } = string.Empty;

    /// <summary>
    /// The string value for this feature at the plan level
    /// (e.g., <c>"100"</c> for numeric, <c>"true"</c> for toggle).
    /// </summary>
    public string Value { get; private set; } = string.Empty;
}
