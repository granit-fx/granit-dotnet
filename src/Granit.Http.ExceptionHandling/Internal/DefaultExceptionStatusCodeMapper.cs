using Granit.Core.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.ExceptionHandling.Internal;

/// <summary>
/// Default <see cref="IExceptionStatusCodeMapper"/> covering Granit exception types
/// and common .NET exception types.
/// Registered last in the chain: returns <c>500</c> for any unrecognized exception.
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
        _ => StatusCodes.Status500InternalServerError
    };
}
