using FluentValidation;

namespace Granit.Validation;

/// <summary>
/// Base class for all Granit validators.
/// </summary>
/// <typeparam name="T">The type being validated.</typeparam>
/// <remarks>
/// Enforces <see cref="CascadeMode.Continue"/> at the class level so that all
/// validation rules are evaluated and all errors are returned in a single response.
/// This prevents the "one error at a time" UX anti-pattern in SPAs.
/// <para>
/// Custom validators should use <c>.WithErrorCodeAndMessage("Validation:...")</c>
/// so that the error code is serialized in <c>ValidationProblemDetails.errors</c>
/// by the Wolverine HTTP middleware.
/// </para>
/// <para>
/// <b>Convention</b>: never call <c>ValidateAndThrow()</c> inside a Wolverine handler.
/// The FluentValidation middleware is the single validation entry point.
/// </para>
/// </remarks>
public abstract class GranitValidator<T> : AbstractValidator<T>
{
    /// <summary>
    /// Initializes a new instance of <see cref="GranitValidator{T}"/>
    /// with <see cref="CascadeMode.Continue"/> to collect all errors.
    /// </summary>
    protected GranitValidator() =>
        ClassLevelCascadeMode = CascadeMode.Continue;
}
