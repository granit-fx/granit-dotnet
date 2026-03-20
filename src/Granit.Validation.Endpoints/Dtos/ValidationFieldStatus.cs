namespace Granit.Validation.Endpoints.Dtos;

/// <summary>
/// Indicates the outcome of a single-field server-side validation.
/// </summary>
public enum ValidationFieldStatus
{
    /// <summary>The value satisfies the validation rule.</summary>
    Valid,

    /// <summary>The value does not satisfy the validation rule.</summary>
    Invalid,

    /// <summary>No validator is registered for the requested error code (batch only).</summary>
    ValidatorNotFound,
}
