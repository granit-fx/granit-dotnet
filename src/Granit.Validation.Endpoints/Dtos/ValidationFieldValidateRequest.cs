namespace Granit.Validation.Endpoints.Dtos;

/// <summary>
/// Request to validate a single field value against a server-side validator.
/// </summary>
/// <param name="ErrorCode">
/// The validator error code (e.g. <c>Validation:InvalidIban</c>).
/// Must match the <c>x-granit-validator</c> extension in the OpenAPI schema.
/// </param>
/// <param name="Value">The value to validate.</param>
public sealed record ValidationFieldValidateRequest(string ErrorCode, string? Value);
