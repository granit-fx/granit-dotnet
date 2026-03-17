using Granit.Http.Idempotency.Internal;
using Microsoft.AspNetCore.Builder;

namespace Granit.Http.Idempotency.Extensions;

/// <summary>
/// Extension methods for adding Granit Idempotency to the ASP.NET Core pipeline.
/// </summary>
public static class IdempotencyApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="IdempotencyMiddleware"/> to the request pipeline.
    /// Must be registered after authentication/authorization middleware so that
    /// <c>ICurrentUserService</c> and <c>ICurrentTenant</c> are populated.
    /// </summary>
    public static IApplicationBuilder UseGranitIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}
