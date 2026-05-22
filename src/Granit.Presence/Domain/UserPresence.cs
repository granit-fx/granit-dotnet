using Granit.Domain;
using Granit.Presence.Events;
using Granit.Timing;

namespace Granit.Presence.Domain;

/// <summary>
/// Persistent presence override for a user. Stores only the manual choice and its
/// optional expiration — the live connectivity signal (last heartbeat) lives in the
/// presence cache, not in this aggregate.
/// </summary>
/// <remarks>
/// Presence is global per user — <see cref="UserId"/> is the primary key and there is
/// no <c>TenantId</c>. A human carries a single presence across every tenant they belong to.
/// </remarks>
public sealed class UserPresence : AuditedAggregateRoot
{
    // Parameterless constructor required by EF Core materializer.
    private UserPresence() { }

    /// <summary>
    /// Creates a new <see cref="UserPresence"/> with no manual override active.
    /// </summary>
    public static UserPresence Create(Guid userId, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        }

        return new UserPresence
        {
            Id = userId,
            UserId = userId,
            ManualStatus = ManualPresenceStatus.Available,
            OverrideUntilUtc = null,
            CreatedAt = clock.Now,
        };
    }

    /// <summary>The user this presence belongs to. Equal to <see cref="Entity.Id"/>.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Current manual override. <see cref="ManualPresenceStatus.Available"/> means none.</summary>
    public ManualPresenceStatus ManualStatus { get; private set; }

    /// <summary>
    /// Expiration of the manual override, or <c>null</c> for indefinite. When the value
    /// is in the past, <see cref="IsOverrideExpired"/> returns <c>true</c> and callers
    /// should treat the override as cleared.
    /// </summary>
    public DateTimeOffset? OverrideUntilUtc { get; private set; }

    /// <summary>
    /// Returns the active manual override if any, or <c>null</c> when the override is
    /// <see cref="ManualPresenceStatus.Available"/> or has expired.
    /// </summary>
    public ManualPresenceStatus? GetActiveOverride(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (ManualStatus == ManualPresenceStatus.Available)
        {
            return null;
        }

        return IsOverrideExpired(clock) ? null : ManualStatus;
    }

    /// <summary>
    /// Sets a manual override. Setting <see cref="ManualPresenceStatus.Available"/>
    /// clears any existing override.
    /// </summary>
    public void SetOverride(ManualPresenceStatus status, DateTimeOffset? untilUtc, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (status == ManualPresenceStatus.Available)
        {
            ClearOverride(clock);
            return;
        }

        ManualPresenceStatus previous = ManualStatus;
        ManualStatus = status;
        OverrideUntilUtc = untilUtc;
        ModifiedAt = clock.Now;

        if (previous != status)
        {
            AddDomainEvent(new UserPresenceOverrideChangedEvent(UserId, previous, status, untilUtc));
        }
    }

    /// <summary>
    /// Clears the manual override; effective status is then derived from connectivity.
    /// </summary>
    public void ClearOverride(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (ManualStatus == ManualPresenceStatus.Available && OverrideUntilUtc is null)
        {
            return;
        }

        ManualPresenceStatus previous = ManualStatus;
        ManualStatus = ManualPresenceStatus.Available;
        OverrideUntilUtc = null;
        ModifiedAt = clock.Now;

        if (previous != ManualPresenceStatus.Available)
        {
            AddDomainEvent(new UserPresenceOverrideChangedEvent(UserId, previous, ManualPresenceStatus.Available, null));
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <see cref="OverrideUntilUtc"/> is set and has elapsed.
    /// </summary>
    public bool IsOverrideExpired(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return OverrideUntilUtc is { } until && until <= clock.Now;
    }
}
