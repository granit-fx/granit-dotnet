namespace Granit.Validation.Endpoints.Dtos;

/// <summary>
/// Request to validate multiple field values in a single round-trip.
/// </summary>
/// <param name="Fields">The fields to validate (max 20).</param>
public sealed record ValidationFieldValidateBatchRequest(IReadOnlyList<ValidationFieldValidateRequest> Fields);
