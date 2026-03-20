namespace Granit.Validation.Endpoints.Dtos;

/// <summary>
/// Response for a single-field server-side validation.
/// </summary>
/// <param name="ErrorCode">The validator error code that was checked.</param>
/// <param name="Status">The validation outcome.</param>
public sealed record ValidationFieldValidateResponse(string ErrorCode, ValidationFieldStatus Status);
