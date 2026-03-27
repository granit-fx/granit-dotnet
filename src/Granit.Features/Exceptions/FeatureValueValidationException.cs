using Granit.Exceptions;

namespace Granit.Features.Exceptions;

/// <summary>
/// Exception thrown when a feature value violates its declared type constraints
/// (e.g. a numeric value outside min/max bounds, or a selection value not in the allowed list).
/// </summary>
public sealed class FeatureValueValidationException : BusinessException
{
    /// <summary>The name of the feature whose value was invalid.</summary>
    public string FeatureName { get; }

    /// <summary>The value that failed validation.</summary>
    public string InvalidValue { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="FeatureValueValidationException"/>.
    /// </summary>
    /// <param name="featureName">The name of the feature.</param>
    /// <param name="invalidValue">The value that failed validation.</param>
    /// <param name="reason">Human-readable validation failure reason.</param>
    public FeatureValueValidationException(string featureName, string invalidValue, string reason)
        : base("Features:InvalidValue", $"Invalid value for feature '{featureName}': {reason}")
    {
        FeatureName = featureName;
        InvalidValue = invalidValue;
    }
}
