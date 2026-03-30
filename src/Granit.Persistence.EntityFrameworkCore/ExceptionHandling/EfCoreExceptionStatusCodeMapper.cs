using Granit.Http.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.ExceptionHandling;

/// <summary>
/// Maps EF Core exceptions to HTTP status codes.
/// Registered as an additional <see cref="IExceptionStatusCodeMapper"/> when both
/// <c>Granit.Persistence.EntityFrameworkCore</c> and <c>Granit.Http.ExceptionHandling</c> are in use.
/// </summary>
/// <remarks>
/// Returns <c>409 Conflict</c> for <see cref="DbUpdateConcurrencyException"/> (optimistic concurrency violation).
/// Returns <c>null</c> for all other exceptions, delegating to the next mapper in the chain.
/// </remarks>
internal sealed class EfCoreExceptionStatusCodeMapper : IExceptionStatusCodeMapper
{
    /// <inheritdoc/>
    public int? TryGetStatusCode(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
        _ => null
    };
}
