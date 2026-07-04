using Granit.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.ExceptionHandling.Internal;

/// <summary>
/// Default <see cref="IExceptionStatusCodeMapper"/> covering Granit exception types
/// and common .NET exception types.
/// Returns <c>null</c> for unrecognized exceptions so downstream mappers
/// (e.g. <c>EfCoreExceptionStatusCodeMapper</c>) can handle them.
/// The <see cref="GranitExceptionHandler"/> falls back to 500 when no mapper matches.
/// </summary>
internal sealed class DefaultExceptionStatusCodeMapper : IExceptionStatusCodeMapper
{
    /// <inheritdoc/>
    public int? TryGetStatusCode(Exception exception) => exception switch
    {
        EntityNotFoundException => StatusCodes.Status404NotFound,
        NotFoundException => StatusCodes.Status404NotFound,
        ForbiddenException => StatusCodes.Status403Forbidden,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        ValidationException => StatusCodes.Status422UnprocessableEntity,
        IHasValidationErrors => StatusCodes.Status422UnprocessableEntity,
        ConflictException => StatusCodes.Status409Conflict,
        BusinessRuleViolationException => StatusCodes.Status422UnprocessableEntity,
        BusinessException => StatusCodes.Status400BadRequest,
        IHasErrorCode => StatusCodes.Status400BadRequest,
        NotImplementedException => StatusCodes.Status501NotImplemented,
        OperationCanceledException => 499,
        TimeoutException => StatusCodes.Status408RequestTimeout,
        // Body binding failures (malformed JSON, payload too large, …) surface as
        // BadHttpRequestException with a status ASP.NET has already resolved
        // (400, 413, …). Honour it instead of hardcoding 400 so a too-large body
        // still maps to 413.
        BadHttpRequestException badRequest => badRequest.StatusCode,
        _ => null
    };
}
