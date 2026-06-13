namespace Granit.Identity;

/// <summary>
/// Durable store for a user's habitual behavioural profile (observed countries / device families / coarse
/// locations). The anomaly detector reads it to suppress <c>new_country</c> / <c>new_device</c> for familiar
/// values, and the session-created handler records each new observation into it.
/// </summary>
/// <remarks>
/// <para>
/// The default registration (<c>Granit.Identity.Abstractions</c>) is in-memory, single-node, and non-durable —
/// suitable for development. Install <c>Granit.Identity.EntityFrameworkCore</c> for a durable store that survives
/// restarts and is shared across instances (otherwise the profile resets on every restart and the false-positive
/// reduction is lost).
/// </para>
/// <para>
/// Lifetime contract mirrors <see cref="IUserSessionRiskStore"/>: depend on this interface from a scoped (or
/// transient) service — never capture it in a singleton. The in-memory default is <c>Singleton</c>; the EF Core
/// store is <c>Scoped</c>.
/// </para>
/// </remarks>
public interface IUserBehavioralProfileStore
{
    /// <summary>Reads the user's full habitual profile, or <see cref="UserBehavioralProfile.Empty"/> when none.</summary>
    Task<UserBehavioralProfile> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an observation: for each non-null/non-empty value, increments its count and updates its
    /// last-seen timestamp (inserting it on first sighting). Pass a <see cref="TimeProvider"/>-derived
    /// <paramref name="observedAt"/>.
    /// </summary>
    Task RecordObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);
}
