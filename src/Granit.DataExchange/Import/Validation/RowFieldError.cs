namespace Granit.DataExchange.Import.Validation;

/// <summary>
/// A single field-level validation error on an imported row.
/// </summary>
/// <param name="PropertyName">The property that failed validation.</param>
/// <param name="ErrorCode">Structured error code (e.g. <c>"Validation:NotEmpty"</c>).</param>
/// <param name="ErrorMessage">Human-readable error message.</param>
public sealed record RowFieldError(
    string PropertyName,
    string ErrorCode,
    string ErrorMessage);
