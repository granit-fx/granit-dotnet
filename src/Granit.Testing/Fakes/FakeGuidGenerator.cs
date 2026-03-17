using Granit.Guids;

namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="IGuidGenerator"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// All state is stored in <see cref="AsyncLocal{T}"/> so that each async context
/// (i.e. each xUnit test) gets isolated values — safe for parallel execution
/// and <c>IClassFixture&lt;T&gt;</c> sharing.
/// </para>
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
/// </remarks>
public sealed class FakeGuidGenerator : IGuidGenerator
{
    private readonly AsyncLocal<GeneratorState?> _state = new();

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
        GeneratorState state = EnsureState();
        foreach (Guid g in guids)
        {
            state.Queue.Enqueue(g);
        }
    }

    /// <inheritdoc/>
    public Guid Create()
    {
        GeneratorState state = EnsureState();
        return state.Queue.Count > 0
            ? state.Queue.Dequeue()
            : CreateSequential(state);
    }

    /// <summary>
    /// Enqueues a GUID to be returned by the next call to <see cref="Create"/>.
    /// </summary>
    /// <param name="value">The GUID to enqueue.</param>
    public void Enqueue(Guid value) => EnsureState().Queue.Enqueue(value);

    private static Guid CreateSequential(GeneratorState state)
    {
        int counter = ++state.Counter;
        return new Guid(counter, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);
    }

    private GeneratorState EnsureState() => _state.Value ??= new GeneratorState();

    private sealed class GeneratorState
    {
        public Queue<Guid> Queue { get; } = new();
        public int Counter { get; set; }
    }
}
