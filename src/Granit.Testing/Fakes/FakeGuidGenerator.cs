using System.Collections.Concurrent;
using Granit.Guids;

namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="IGuidGenerator"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// Dual-mode operation:
/// <list type="bullet">
///   <item>
///     <b>Queue mode:</b> Dequeues GUIDs enqueued via <see cref="Enqueue"/>.
///     Use this when tests need exact GUID values for assertions.
///   </item>
///   <item>
///     <b>Sequential mode:</b> Falls back to deterministic sequential GUIDs
///     (<c>00000001-0000-0000-0000-000000000000</c>, etc.) when the queue is empty.
///   </item>
/// </list>
/// </para>
/// <para>
/// State is stored on the instance (not in <see cref="AsyncLocal{T}"/>), so the
/// counter and queue survive across <c>await</c> continuations regardless of
/// <c>ConfigureAwait(false)</c>. Isolation between parallel tests is guaranteed by
/// each test creating its own instance — <c>AsyncLocal</c> would add no benefit here
/// and would silently reset the counter every time a continuation resumes on a new
/// execution context.
/// </para>
/// </remarks>
public sealed class FakeGuidGenerator : IGuidGenerator
{
    private readonly ConcurrentQueue<Guid> _queue = new();
    private int _counter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeGuidGenerator"/> class.
    /// </summary>
    public FakeGuidGenerator()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeGuidGenerator"/> class
    /// with pre-enqueued GUIDs.
    /// </summary>
    /// <param name="guids">GUIDs to return in order from <see cref="Create"/>.</param>
    public FakeGuidGenerator(params ReadOnlySpan<Guid> guids)
    {
        foreach (Guid g in guids)
        {
            _queue.Enqueue(g);
        }
    }

    /// <inheritdoc/>
    public Guid Create()
    {
        if (_queue.TryDequeue(out Guid queued))
        {
            return queued;
        }

        int n = Interlocked.Increment(ref _counter);
        return new Guid(n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }

    /// <summary>
    /// Enqueues a GUID to be returned by the next call to <see cref="Create"/>.
    /// </summary>
    /// <param name="value">The GUID to enqueue.</param>
    public void Enqueue(Guid value) => _queue.Enqueue(value);
}
