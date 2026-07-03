namespace Granit.Timing;

/// <summary>
/// Default implementation of <see cref="IClock"/>.
/// Uses <see cref="TimeProvider"/> for time access
/// and <see cref="ICurrentTimezoneProvider"/> for timezone conversions.
/// </summary>
public sealed class Clock(TimeProvider timeProvider, ICurrentTimezoneProvider timezoneProvider) : IClock
{
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ICurrentTimezoneProvider _timezoneProvider = timezoneProvider;

    /// <inheritdoc />
    public DateTimeOffset Now => _timeProvider.GetUtcNow();

    /// <inheritdoc />
    public bool SupportsMultipleTimezone => true;

    /// <inheritdoc />
    public DateTimeOffset Normalize(DateTimeOffset dateTime) =>
        // ISO 27001 compliance: all values are converted to UTC before persistence.
        // Even a DateTimeOffset with a local offset (+02:00) is normalized to UTC (+00:00).
        dateTime.ToUniversalTime();

    /// <inheritdoc />
    public DateTimeOffset ConvertToUserTime(DateTimeOffset utcDateTime) =>
        TimeZoneInfo.ConvertTime(utcDateTime, ResolveUserTimeZone());

    /// <inheritdoc />
    public DateTimeOffset ConvertToUtc(DateTimeOffset dateTime) => dateTime.ToUniversalTime();

    /// <inheritdoc />
    public TimeZoneInfo ResolveUserTimeZone()
    {
        string? tz = _timezoneProvider.Timezone;

        // No configured timezone (or an unknown identifier) → treat the user as operating in UTC.
        return !string.IsNullOrWhiteSpace(tz) && TimeZoneInfo.TryFindSystemTimeZoneById(tz, out TimeZoneInfo? tzInfo)
            ? tzInfo
            : TimeZoneInfo.Utc;
    }

    /// <inheritdoc />
    public DateTimeOffset ToUtcFromUserLocal(DateTime wallClock)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        DateTime utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, ResolveUserTimeZone());
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
