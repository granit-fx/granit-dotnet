namespace Granit.Features.Endpoints.Dtos;

/// <summary>
/// Response representing a single feature definition.
/// </summary>
/// <param name="Name">Unique feature name (e.g. <c>"Acme.VideoConference"</c>).</param>
/// <param name="DefaultValue">Default value when no override is set.</param>
/// <param name="ValueType">Storage type: <c>"Toggle"</c>, <c>"Numeric"</c>, or <c>"Selection"</c>.</param>
/// <param name="NumericConstraint">Min/max bounds for numeric features, or <c>null</c>.</param>
/// <param name="SelectionValues">Allowed values for selection features, or <c>null</c>.</param>
/// <param name="DisplayName">Display label for admin UI, or <c>null</c>.</param>
/// <param name="Description">Long description, or <c>null</c>.</param>
public sealed record FeatureDefinitionResponse(
    string Name,
    string DefaultValue,
    string ValueType,
    FeatureNumericConstraintResponse? NumericConstraint,
    IReadOnlyList<string>? SelectionValues,
    string? DisplayName,
    string? Description);

/// <summary>
/// Numeric constraint bounds for a feature definition.
/// </summary>
/// <param name="Min">Minimum allowed value (inclusive).</param>
/// <param name="Max">Maximum allowed value (inclusive).</param>
public sealed record FeatureNumericConstraintResponse(long Min, long Max);
