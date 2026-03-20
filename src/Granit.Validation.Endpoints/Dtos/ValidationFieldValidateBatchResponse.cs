namespace Granit.Validation.Endpoints.Dtos;

/// <summary>
/// Response for a batch field validation.
/// </summary>
/// <param name="Results">
/// Validation results in the same order as the request fields.
/// Fields with an unknown error code have <see cref="ValidationFieldStatus.ValidatorNotFound"/>.
/// </param>
public sealed record ValidationFieldValidateBatchResponse(IReadOnlyList<ValidationFieldValidateResponse> Results);
