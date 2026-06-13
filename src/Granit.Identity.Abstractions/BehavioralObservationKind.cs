namespace Granit.Identity;

/// <summary>
/// The dimension of a recorded behavioural observation in a user's durable habitual profile. The anomaly
/// detector consults the profile so a value seen repeatedly (or recently) is not re-flagged as new on every visit.
/// </summary>
public enum BehavioralObservationKind
{
    /// <summary>An ISO 3166-1 alpha-2 country code the user has authenticated from.</summary>
    Country,

    /// <summary>A coarse device family (browser/OS class) derived from the User-Agent.</summary>
    DeviceFamily,

    /// <summary>A coarse geographic bucket (rounded latitude/longitude) the user has authenticated from.</summary>
    CoarseLocation,
}
