namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="TimeProvider"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// State is stored on the instance (not in <see cref="AsyncLocal{T}"/>), so the time
/// value survives across <c>await</c> continuations regardless of
/// <c>ConfigureAwait(false)</c> and is visible from sibling execution contexts
/// (e.g. xUnit v3 where <c>IAsyncLifetime.InitializeAsync</c> and the test method
/// run in independent contexts, not parent-child). Isolation between parallel tests
/// is guaranteed by each test creating its own instance.
/// </para>
/// <para>
/// Default time is <c>2026-01-15T10:00:00Z</c>. Use <see cref="Advance"/> or
/// <see cref="SetUtcNow"/> to control time in temporal test scenarios.
/// </para>
/// </remarks>
public sealed class FakeTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _utcNow;

    /// <summary>
    /// Initializes a new instance with the default time (<c>2026-01-15T10:00:00Z</c>).
    /// </summary>
    public FakeTimeProvider() => _utcNow = DefaultNow;

    /// <summary>
    /// Initializes a new instance pinned to <paramref name="utcNow"/>.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _utcNow;

    /// <summary>
    /// Sets the current UTC time returned by <see cref="GetUtcNow"/>.
    /// </summary>
    public void SetUtcNow(DateTimeOffset value) => _utcNow = value;

    /// <summary>
    /// Advances the current time by <paramref name="duration"/>.
    /// </summary>
    public void Advance(TimeSpan duration) => _utcNow += duration;
}
