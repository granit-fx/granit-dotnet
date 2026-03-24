namespace Granit.Exceptions;

/// <summary>
/// Indicates that an exception carries structured field-level validation errors.
/// <c>GranitExceptionHandler</c> maps this to a <c>422 Unprocessable Entity</c> response
/// and populates <c>ProblemDetails.Extensions["errors"]</c> with the validation error dictionary.
/// </summary>
public interface IHasValidationErrors
{
    /// <summary>
    /// Validation errors keyed by field name, each with one or more error messages.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///   "Email": ["The Email field is required."],
    ///   "Name":  ["Must be between 3 and 50 characters."]
    /// }
    /// </code>
    /// </example>
    IReadOnlyDictionary<string, string[]> ValidationErrors { get; }
}
