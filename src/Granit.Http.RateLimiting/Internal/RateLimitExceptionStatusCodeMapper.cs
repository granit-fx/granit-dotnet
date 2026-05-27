using Granit.Http.ExceptionHandling;
using Granit.RateLimiting.Exceptions;
using Microsoft.AspNetCore.Http;

namespace Granit.Http.RateLimiting.Internal;

/// <summary>
/// Maps <see cref="RateLimitExceededException"/> to HTTP 429 Too Many Requests.
/// </summary>
internal sealed class RateLimitExceptionStatusCodeMapper : IExceptionStatusCodeMapper
{
    /// <inheritdoc/>
    public int? TryGetStatusCode(Exception exception) =>
        exception is RateLimitExceededException ? StatusCodes.Status429TooManyRequests : null;
}
