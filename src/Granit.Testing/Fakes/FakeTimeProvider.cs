namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="TimeProvider"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// All state is stored in <see cref="AsyncLocal{T}"/> so that each async context
/// (i.e. each xUnit test) gets isolated values — safe for parallel execution
/// and <c>IClassFixture&lt;T&gt;</c> sharing.
/// </para>
/// <para>
/// Default time is <c>2026-01-15T10:00:00Z</c>. Use <see cref="Advance"/> or
/// <see cref="SetUtcNow"/> to control time in temporal test scenarios.
/// </para>
/// </remarks>
public sealed class FakeTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly AsyncLocal<DateTimeOffset?> _utcNow = new();

    /// <summary>
    /// Initializes a new instance with the default time (<c>2026-01-15T10:00:00Z</c>).
    /// </summary>
    public FakeTimeProvider() { }

    /// <summary>
    /// Initializes a new instance pinned to <paramref name="utcNow"/>.
    /// </summary>
    public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow.Value = utcNow;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => _utcNow.Value ?? DefaultNow;

    /// <summary>
    /// Sets the current UTC time returned by <see cref="GetUtcNow"/>.
    /// </summary>
    public void SetUtcNow(DateTimeOffset value) => _utcNow.Value = value;

    /// <summary>
    /// Advances the current time by <paramref name="duration"/>.
    /// </summary>
    public void Advance(TimeSpan duration) => _utcNow.Value = GetUtcNow().Add(duration);
}
