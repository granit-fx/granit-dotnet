namespace Granit.Timing;

/// <summary>
/// Abstraction for accessing system time and performing timezone conversions.
/// Uses <see cref="TimeProvider"/> internally for <see cref="Now"/>.
/// </summary>
public interface IClock
{
    /// <summary>Gets the current instant in UTC.</summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Indicates whether the Clock supports multiple timezones.
    /// Returns <c>true</c> when the Clock operates in UTC (standard ISO 27001 case).
    /// </summary>
    bool SupportsMultipleTimezone { get; }

    /// <summary>
    /// Normalizes a <see cref="DateTimeOffset"/> to UTC.
    /// Ensures that even a local offset (+02:00) is converted to UTC (+00:00)
    /// before persistence.
    /// </summary>
    DateTimeOffset Normalize(DateTimeOffset dateTime);

    /// <summary>
    /// Converts a UTC <see cref="DateTimeOffset"/> to the current user's timezone
    /// (via <see cref="ICurrentTimezoneProvider"/>).
    /// If no timezone is configured, returns the value unchanged.
    /// </summary>
    DateTimeOffset ConvertToUserTime(DateTimeOffset utcDateTime);

    /// <summary>
    /// Converts a <see cref="DateTimeOffset"/> from the user's timezone to UTC.
    /// If no timezone is configured, returns the value unchanged.
    /// </summary>
    DateTimeOffset ConvertToUtc(DateTimeOffset dateTime);

    /// <summary>
    /// Resolves the current user's timezone (via <see cref="ICurrentTimezoneProvider"/>),
    /// falling back to <see cref="TimeZoneInfo.Utc"/> when none is configured or the
    /// identifier is unknown.
    /// </summary>
    TimeZoneInfo ResolveUserTimeZone();

    /// <summary>
    /// Converts a wall-clock local time — interpreted in the current user's timezone
    /// (<see cref="ResolveUserTimeZone"/>) — to the corresponding UTC instant, honouring
    /// DST transitions. Unlike <see cref="ConvertToUtc"/> (which applies a fixed offset),
    /// this handles a constructed local time (e.g. local midnight), where a "day" may span
    /// 23 or 25 hours across a transition.
    /// </summary>
    /// <param name="wallClock">
    /// The local wall-clock time. Its <see cref="DateTime.Kind"/> is ignored (treated as
    /// <see cref="DateTimeKind.Unspecified"/>).
    /// </param>
    /// <remarks>
    /// Ambiguous (fall-back) and invalid (spring-forward gap) local times follow the default
    /// <see cref="TimeZoneInfo.ConvertTimeToUtc(DateTime, TimeZoneInfo)"/> behaviour: ambiguous
    /// times resolve to standard (non-DST) time; an invalid time throws
    /// <see cref="ArgumentException"/>. No custom disambiguation rule is applied.
    /// </remarks>
    DateTimeOffset ToUtcFromUserLocal(DateTime wallClock);
}
