using System.Collections.Concurrent;

namespace Granit.Dashboards.Push.Internal;

/// <summary>
/// Allocates monotonic per-<c>(tenantId, widgetInstanceId)</c> sequence numbers
/// for the framework push transport. ADR-043 §5 — locked by EPIC #1366
/// invariant #2 ("<c>Sequence</c> is monotonic per <c>(widget, tenant)</c>;
/// pull mode = always <c>1</c>, push transport increments on every emit").
/// </summary>
/// <remarks>
/// <para>
/// In-memory implementation backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// of mutable boxes. <see cref="Interlocked.Increment(ref long)"/> on each box
/// gives lock-free per-key allocation.
/// </para>
/// <para>
/// Single-host deployments are the v1 target — multi-host deployments either
/// front the framework with a single stream-handler instance or DI-replace
/// this allocator with a Redis / database-backed counter. The interface stays
/// the same so the SSE endpoint never sees the difference.
/// </para>
/// </remarks>
internal sealed class WidgetPushSequenceAllocator
{
    private readonly ConcurrentDictionary<SequenceKey, Counter> _counters = new();

    /// <summary>
    /// Returns the next sequence number for <c>(tenantId, widgetInstanceId)</c>.
    /// Sequences start at 1 — pull-mode envelopes also use 1, so a fresh subscriber
    /// that pulled a seed (sequence 1) immediately sees sequence 2 on the first
    /// pushed update.
    /// </summary>
    public long Next(Guid? tenantId, Guid widgetInstanceId)
    {
        Counter counter = _counters.GetOrAdd(new SequenceKey(tenantId, widgetInstanceId), _ => new Counter());
        return Interlocked.Increment(ref counter.Value);
    }

    private readonly record struct SequenceKey(Guid? TenantId, Guid WidgetInstanceId);

    private sealed class Counter
    {
        // Starts at 0; first Interlocked.Increment returns 1, matching the pull-mode seed.
        public long Value;
    }
}
