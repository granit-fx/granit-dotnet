namespace Granit.Identity;

/// <summary>
/// One entry in a user's durable habitual profile: a value seen for a given dimension, with how often and how
/// recently it has been observed.
/// </summary>
/// <param name="Kind">The dimension this value belongs to.</param>
/// <param name="Value">The observed value (e.g. a country code, device family, or coarse-location bucket).</param>
/// <param name="Count">How many times the value has been observed.</param>
/// <param name="FirstSeenAt">When the value was first observed.</param>
/// <param name="LastSeenAt">When the value was most recently observed.</param>
public sealed record BehavioralObservation(
    BehavioralObservationKind Kind,
    string Value,
    int Count,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);
