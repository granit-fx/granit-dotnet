namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core entity persisting one entry of a user's durable habitual profile — a value observed for a given
/// dimension, with frequency and recency — keyed by <c>(UserId, Kind, Value)</c>.
/// </summary>
internal sealed class UserBehavioralProfileEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Subject the observation belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Dimension of the observed value. Persisted as its PascalCase name (for SQL-audit readability) by the
    /// Granit enum-as-string convention applied via <c>ApplyGranitConventions</c>.
    /// </summary>
    public BehavioralObservationKind Kind { get; set; }

    /// <summary>The observed value (country code, device family, or coarse-location bucket).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>How many times the value has been observed.</summary>
    public int Count { get; set; }

    /// <summary>When the value was first observed.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>When the value was most recently observed.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
