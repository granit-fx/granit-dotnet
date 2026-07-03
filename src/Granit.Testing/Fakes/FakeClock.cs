using Granit.Timing;

namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="IClock"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// All state is stored in <see cref="AsyncLocal{T}"/> so that each async context
/// (i.e. each xUnit test) gets isolated values — safe for parallel execution
/// and <c>IClassFixture&lt;T&gt;</c> sharing.
/// </para>
/// <para>
/// Default time is <c>2026-01-15T10:00:00Z</c>. Use <see cref="Advance"/> to
/// move time forward in temporal test scenarios.
/// </para>
/// </remarks>
public sealed class FakeClock : IClock
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly AsyncLocal<DateTimeOffset?> _now = new();

    /// <inheritdoc/>
    public DateTimeOffset Now
    {
        get => _now.Value ?? DefaultNow;
        set => _now.Value = value;
    }

    /// <inheritdoc/>
    public bool SupportsMultipleTimezone => false;

    /// <inheritdoc/>
    public DateTimeOffset Normalize(DateTimeOffset dateTime) => dateTime.ToUniversalTime();

    /// <inheritdoc/>
    public DateTimeOffset ConvertToUserTime(DateTimeOffset utcDateTime) => utcDateTime;

    /// <inheritdoc/>
    public DateTimeOffset ConvertToUtc(DateTimeOffset dateTime) => dateTime.ToUniversalTime();

    /// <inheritdoc/>
    public TimeZoneInfo ResolveUserTimeZone() => TimeZoneInfo.Utc;

    /// <inheritdoc/>
    public DateTimeOffset ToUtcFromUserLocal(DateTime wallClock) =>
        new(DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified), TimeSpan.Zero);

    /// <summary>
    /// Advances the clock by the specified duration.
    /// </summary>
    /// <param name="duration">The amount of time to advance.</param>
    public void Advance(TimeSpan duration) => Now = Now.Add(duration);
}
