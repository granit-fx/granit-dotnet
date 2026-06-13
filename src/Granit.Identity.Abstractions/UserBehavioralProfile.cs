namespace Granit.Identity;

/// <summary>
/// A user's durable habitual profile — the distinct countries, device families, and coarse locations they have
/// authenticated from over time, with recency/frequency. Unlike the set of currently-active sessions (which a
/// monthly second residence drops out of between visits), this persists, so the anomaly detector can tell a
/// habitual location/device from a genuinely new one and stop re-flagging the familiar one as <c>new_country</c>
/// / <c>new_device</c> on every visit.
/// </summary>
/// <param name="Observations">Every recorded observation, across all dimensions.</param>
public sealed record UserBehavioralProfile(IReadOnlyList<BehavioralObservation> Observations)
{
    /// <summary>An empty profile — the value returned for a user with no recorded history.</summary>
    public static UserBehavioralProfile Empty { get; } = new([]);

    /// <summary>
    /// Whether <paramref name="value"/> is a habitual value for <paramref name="kind"/> as of
    /// <paramref name="now"/>: observed at least <paramref name="minObservations"/> times, or seen within
    /// <paramref name="recencyWindow"/>. Observations last seen longer ago than <paramref name="retention"/> are
    /// treated as expired and never count. Callers pass a <see cref="TimeProvider"/>-derived
    /// <paramref name="now"/> — never <c>DateTimeOffset.UtcNow</c> directly.
    /// </summary>
    public bool IsHabitual(
        BehavioralObservationKind kind,
        string value,
        DateTimeOffset now,
        int minObservations,
        TimeSpan recencyWindow,
        TimeSpan retention)
    {
        foreach (BehavioralObservation observation in Observations)
        {
            if (observation.Kind != kind
                || !string.Equals(observation.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (now - observation.LastSeenAt > retention)
            {
                return false;
            }

            return observation.Count >= minObservations || now - observation.LastSeenAt <= recencyWindow;
        }

        return false;
    }
}
