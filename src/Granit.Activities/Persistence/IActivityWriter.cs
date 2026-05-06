using Granit.Activities.Domain;

namespace Granit.Activities.Persistence;

/// <summary>
/// Write operations on the activity aggregate. Each method loads the row,
/// invokes the corresponding behavior on <see cref="Activity"/>, and persists
/// the change in a single SaveChanges call (events flow through the standard
/// EF Core lifecycle interceptors).
/// </summary>
/// <remarks>
/// Hosts wire <c>EfCoreActivityWriter</c> via
/// <c>AddGranitActivitiesEntityFrameworkCore()</c>; tests can substitute a fake.
/// All write methods throw <see cref="InvalidOperationException"/> when the
/// targeted activity is in a terminal state — append-only contract per
/// ADR-046.
/// </remarks>
public interface IActivityWriter
{
    /// <summary>Creates a new <see cref="Activity"/> in <see cref="ActivityStatus.Open"/>.</summary>
    Task<Activity> CreateAsync(
        string entityType,
        Guid entityId,
        string type,
        Guid assignedToUserId,
        DateTimeOffset dueAt,
        Guid? createdByUserId = null,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the activity as <see cref="ActivityStatus.Done"/>.</summary>
    Task CompleteAsync(Guid activityId, Guid completedByUserId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Marks the activity as <see cref="ActivityStatus.Cancelled"/>.</summary>
    Task CancelAsync(Guid activityId, Guid cancelledByUserId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Reassigns the activity to a different user (allowed only while open).</summary>
    Task ReassignAsync(Guid activityId, Guid newAssigneeUserId, CancellationToken cancellationToken = default);

    /// <summary>Updates <see cref="Activity.DueAt"/> (allowed only while open).</summary>
    Task RescheduleAsync(Guid activityId, DateTimeOffset newDueAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamps <see cref="Activity.OverdueNotifiedAt"/> on the row, recording
    /// that the overdue background job (story A8) has emitted its notification
    /// for this activity. Idempotent — calling on an already-stamped row
    /// preserves the original timestamp.
    /// </summary>
    Task MarkOverdueNotifiedAsync(Guid activityId, DateTimeOffset at, CancellationToken cancellationToken = default);
}
