namespace Granit.Validation.ServerValidation;

/// <summary>
/// Validates a single string value against a server-side rule identified by its error code.
/// </summary>
/// <remarks>
/// Implementations are auto-discovered from all loaded module assemblies via
/// <see cref="IServerValidatorContributor"/>. The error code must match the
/// <c>x-granit-validator</c> extension emitted in the OpenAPI schema by
/// <c>FluentValidationSchemaTransformer</c>.
/// </remarks>
public interface IServerValidator
{
    /// <summary>
    /// The <c>Granit:Validation:*</c> error code that uniquely identifies this validator.
    /// </summary>
    string ErrorCode { get; }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="value"/> satisfies the validation rule.
    /// </summary>
    bool Validate(string? value);
}
