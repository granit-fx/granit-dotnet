using Granit.Features.Exceptions;

namespace Granit.Features.ValueTypes;

/// <summary>
/// Defines the min/max bounds for a <see cref="FeatureValueType.Numeric"/> feature.
/// </summary>
/// <param name="Min">Minimum allowed value (inclusive).</param>
/// <param name="Max">Maximum allowed value (inclusive).</param>
public sealed record NumericConstraint(long Min, long Max)
{
    /// <summary>
    /// Validates that <paramref name="rawValue"/> is a valid integer within [Min, Max].
    /// </summary>
    /// <param name="featureName">Feature name for error messages.</param>
    /// <param name="rawValue">The string value to validate.</param>
    /// <exception cref="FeatureValueValidationException">
    /// Thrown when the value is not a valid integer or falls outside [Min, Max].
    /// </exception>
    public void Validate(string featureName, string rawValue)
    {
        if (!long.TryParse(rawValue, out long parsed))
        {
            throw new FeatureValueValidationException(
                featureName,
                rawValue,
                "value must be a valid integer.");
        }

        if (parsed < Min || parsed > Max)
        {
            throw new FeatureValueValidationException(
                featureName,
                rawValue,
                $"value must be between {Min} and {Max} (got {parsed}).");
        }
    }
}
