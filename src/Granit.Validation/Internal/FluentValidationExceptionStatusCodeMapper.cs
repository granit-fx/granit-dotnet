using FluentValidation;
using Granit.Http.ExceptionHandling;
using Microsoft.AspNetCore.Http;

namespace Granit.Validation.Internal;

/// <summary>
/// Maps <see cref="FluentValidation.ValidationException"/> to HTTP 422 Unprocessable Entity.
/// </summary>
/// <remarks>
/// Registered in the <see cref="GranitExceptionHandling.IExceptionStatusCodeMapper"/> chain by
/// <see cref="GranitValidationModule"/>. It must be registered <b>before</b> the
/// <c>DefaultExceptionStatusCodeMapper</c> fallback so that it wins on
/// <see cref="FluentValidation.ValidationException"/>.
/// <para>
/// <b>Note:</b> On the HTTP path, <c>WolverineFx.Http.FluentValidation</c> short-circuits
/// the pipeline and writes a 400 <c>ValidationProblemDetails</c> response directly — this
/// mapper is never invoked on that path. It covers the edge case where
/// <see cref="FluentValidation.ValidationException"/> is raised outside the Wolverine HTTP
/// middleware (e.g. programmatic usage of validators).
/// </para>
/// </remarks>
internal sealed class FluentValidationExceptionStatusCodeMapper : IExceptionStatusCodeMapper
{
    /// <inheritdoc/>
    public int? TryGetStatusCode(Exception exception) =>
        exception is ValidationException ? StatusCodes.Status422UnprocessableEntity : null;
}
