using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Exceptions;
using Granit.RateLimiting.Wolverine.Attributes;

namespace Granit.RateLimiting.Wolverine;

/// <summary>
/// Wolverine pipeline middleware that enforces <see cref="RateLimitedAttribute"/> checks
/// before the message handler executes.
/// </summary>
/// <remarks>
/// Wolverine discovers this middleware by convention (class with a static <c>BeforeAsync</c> method).
/// Register it in your Wolverine setup:
/// <code>
/// opts.Policies.AddMiddleware&lt;RateLimitMiddleware&gt;(
///     chain => chain.MessageType.GetCustomAttributes(typeof(RateLimitedAttribute), true).Length > 0);
/// </code>
/// When the rate limit is exceeded, <see cref="RateLimitExceededException"/> is thrown
/// and handled by Wolverine's retry policy (RetryWithCooldown).
/// </remarks>
public static class RateLimitMiddleware
{
    /// <summary>
    /// Wolverine "before" hook — invoked before the message handler.
    /// Reads the <see cref="RateLimitedAttribute"/> decorating the message type and
    /// checks the rate limit counter.
    /// </summary>
    /// <param name="message">The incoming message whose type is inspected for attributes.</param>
    /// <param name="limiter">Resolved from the DI container by Wolverine.</param>
    /// <param name="cancellationToken">Cancellation token propagated by Wolverine from the transport.</param>
    public static async Task BeforeAsync(
        object message,
        TenantPartitionedRateLimiter limiter,
        CancellationToken cancellationToken)
    {
        RateLimitedAttribute? attribute = message.GetType()
            .GetCustomAttributes(typeof(RateLimitedAttribute), inherit: true)
            .Cast<RateLimitedAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            return;
        }

        RateLimitResult? result = await limiter.CheckAsync(attribute.PolicyName, clientIp: null, cancellationToken)
            .ConfigureAwait(false);

        if (result is { IsAllowed: false })
        {
            throw new RateLimitExceededException(
                attribute.PolicyName, result.RetryAfter, result.Limit, result.Remaining);
        }
    }
}
