using Granit.Features.Definitions;
using Granit.Features.Endpoints.Dtos;
using Granit.Features.Exceptions;
using Granit.Features.ValueTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Features.Endpoints.Internal;

/// <summary>
/// Maps feature domain objects to response DTOs and provides shared endpoint helpers.
/// </summary>
internal static class FeaturesResponseMapper
{
    /// <summary>Returns a 404 Problem result indicating the feature is not declared.</summary>
    public static ProblemHttpResult FeatureNotFound() =>
        TypedResults.Problem(
            detail: "The requested feature is not declared in any definition provider.",
            statusCode: StatusCodes.Status404NotFound);

    /// <summary>Maps a <see cref="FeatureDefinition"/> to its response DTO.</summary>
    public static FeatureDefinitionResponse MapDefinition(FeatureDefinition definition) =>
        new(
            definition.Name,
            definition.DefaultValue,
            definition.ValueType.ToString(),
            definition.NumericConstraint is { } nc
                ? new FeatureNumericConstraintResponse(nc.Min, nc.Max)
                : null,
            definition.SelectionValues?.AllowedValues,
            definition.DisplayName,
            definition.Description);

    /// <summary>
    /// Extracts the group name from a feature name following the <c>"Group.Feature"</c>
    /// naming convention. Returns the full name if no dot is present.
    /// </summary>
    public static string ExtractGroupName(string featureName)
    {
        int dotIndex = featureName.IndexOf('.');
        return dotIndex > 0 ? featureName[..dotIndex] : featureName;
    }

    /// <summary>
    /// Validates that <paramref name="value"/> is compatible with the feature's declared value type.
    /// Throws <see cref="FeatureValueValidationException"/> when the value is invalid.
    /// </summary>
    public static void ValidateValueType(FeatureDefinition definition, string value)
    {
        switch (definition.ValueType)
        {
            case FeatureValueType.Toggle:
                if (!string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FeatureValueValidationException(
                        definition.Name,
                        value,
                        "value must be 'true' or 'false'.");
                }

                break;

            case FeatureValueType.Numeric:
                definition.NumericConstraint?.Validate(definition.Name, value);

                if (definition.NumericConstraint is null && !long.TryParse(value, out _))
                {
                    throw new FeatureValueValidationException(
                        definition.Name,
                        value,
                        "value must be a valid integer.");
                }

                break;

            case FeatureValueType.Selection:
                definition.SelectionValues?.Validate(definition.Name, value);
                break;
        }
    }
}
