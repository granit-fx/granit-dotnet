namespace Granit.Exceptions;

/// <summary>
/// Exception thrown when one or more input validation rules are violated.
/// Maps to <c>422 Unprocessable Entity</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IHasValidationErrors"/>: the handler will populate
/// <c>ProblemDetails.Extensions["errors"]</c> with the structured field-level errors.
/// Implements <see cref="IUserFriendlyException"/>: field errors are safe for client display.
/// </remarks>
/// <example>
/// <code>
/// IReadOnlyDictionary&lt;string, string[]&gt; errors = new Dictionary&lt;string, string[]&gt;
/// {
///     ["Email"] = ["The Email field is required."],
///     ["Name"]  = ["Must be between 3 and 50 characters."]
/// };
/// throw new ValidationException(errors);
/// </code>
/// </example>
public sealed class ValidationException : Exception, IHasValidationErrors, IUserFriendlyException
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string[]> ValidationErrors { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ValidationException"/>.
    /// </summary>
    /// <param name="validationErrors">
    /// Field-level validation errors. Keys are field names; values are arrays of error messages.
    /// </param>
    public ValidationException(IReadOnlyDictionary<string, string[]> validationErrors)
        : base("One or more validation errors occurred.")
    {
        ValidationErrors = validationErrors;
    }
}
