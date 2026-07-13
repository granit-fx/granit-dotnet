using Granit.Bulkhead.Abstractions;
using Granit.Bulkhead.Wolverine.Attributes;

namespace Granit.Bulkhead.Wolverine;

/// <summary>
/// Wolverine pipeline middleware that enforces <see cref="BulkheadAttribute"/> checks
/// before the message handler executes and releases the lease after.
/// </summary>
/// <remarks>
/// <para>
/// Wolverine discovers this middleware by convention (class with static <c>BeforeAsync</c>/<c>After</c> methods).
/// Register it in your Wolverine setup:
/// </para>
/// <code>
/// opts.Policies.AddMiddleware&lt;BulkheadMiddleware&gt;(
///     chain => chain.MessageType.GetCustomAttributes(typeof(BulkheadAttribute), true).Length > 0);
/// </code>
/// <para>
/// <c>BeforeAsync</c> returns a <see cref="BulkheadLease"/>. Wolverine automatically injects
/// this return value into <c>After</c> as a parameter. This avoids <c>AsyncLocal</c> and
/// guarantees proper disposal even if the handler throws.
/// </para>
/// <para>
/// When the bulkhead is full, <see cref="Exceptions.BulkheadRejectedException"/> is thrown
/// and handled by Wolverine's retry policy (RetryWithCooldown).
/// </para>
/// </remarks>
public static class BulkheadMiddleware
{
    /// <summary>
    /// Wolverine "before" hook — invoked before the message handler.
    /// Reads the <see cref="BulkheadAttribute"/> decorating the message type and
    /// acquires a concurrency permit.
    /// </summary>
    /// <param name="message">The incoming message whose type is inspected for attributes.</param>
    /// <param name="bulkhead">Resolved from the DI container by Wolverine.</param>
    /// <param name="cancellationToken">Cancellation token propagated by Wolverine from the transport.</param>
    /// <returns>A <see cref="BulkheadLease"/> that Wolverine passes to <see cref="After"/>.</returns>
    public static async Task<BulkheadLease> BeforeAsync(
        object message,
        TenantPartitionedBulkhead bulkhead,
        CancellationToken cancellationToken)
    {
        BulkheadAttribute? attribute = message.GetType()
            .GetCustomAttributes(typeof(BulkheadAttribute), inherit: true)
            .Cast<BulkheadAttribute>()
            .FirstOrDefault();

        if (attribute is null)
        {
            return BulkheadLease.NoOp;
        }

        return await bulkhead.AcquireAsync(attribute.PolicyName, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Wolverine "after" hook — invoked after the message handler completes (success or failure).
    /// Releases the concurrency permit back to the bulkhead.
    /// </summary>
    /// <param name="lease">The lease returned by <see cref="BeforeAsync"/>, injected by Wolverine.</param>
    public static void After(BulkheadLease lease) => lease.Dispose();
}
