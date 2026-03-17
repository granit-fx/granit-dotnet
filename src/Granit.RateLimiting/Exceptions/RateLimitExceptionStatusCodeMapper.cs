using Granit.Http.ExceptionHandling;
using Microsoft.AspNetCore.Http;

namespace Granit.RateLimiting.Exceptions;

/// <summary>
/// Maps <see cref="RateLimitExceededException"/> to HTTP 429 Too Many Requests.
/// </summary>
internal sealed class RateLimitExceptionStatusCodeMapper : IExceptionStatusCodeMapper
{
    /// <inheritdoc/>
    public int? TryGetStatusCode(Exception exception) =>
        exception is RateLimitExceededException ? StatusCodes.Status429TooManyRequests : null;
}
