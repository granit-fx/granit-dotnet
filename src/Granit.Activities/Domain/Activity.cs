using Granit.Activities.Events;
using Granit.Domain;

namespace Granit.Activities.Domain;

/// <summary>
/// Cross-entity polymorphic to-do (ADR-046 §2). Polymorphic on
/// (<see cref="EntityType"/>, <see cref="EntityId"/>) so any entity in the system
/// can host activities without coupling — same shape as <c>Granit.Timeline</c>.
/// </summary>
/// <remarks>
/// <para>
/// Behavior: created via <see cref="Create"/>, then transitions through
/// <see cref="Reassign"/> / <see cref="Reschedule"/> while open, and terminates
/// in either <see cref="Complete"/> or <see cref="Cancel"/>. Once terminal,
/// further behavior calls throw <see cref="InvalidOperationException"/> —
/// activities are not re-opened.
/// </para>
/// <para>
/// Type validation is delegated to <see cref="IActivityRegistry"/> in
/// <see cref="Create"/> / <see cref="Reassign"/> calls — types from providers
/// the host doesn't load are rejected at the factory boundary, never
/// silently persisted.
/// </para>
/// </remarks>
public sealed class Activity : FullAuditedAggregateRoot, IMultiTenant, IEmitEntityLifecycleEvents
{
    // Parameterless constructor required by EF Core materializer (story A3).
    private Activity() { }

    /// <summary>
    /// Creates a new <see cref="Activity"/> in <see cref="ActivityStatus.Open"/>
    /// state. Validates <paramref name="type"/> against the registry; raises
    /// <see cref="ActivityAssignedEvent"/> + <see cref="ActivityAssignedEto"/>.
    /// </summary>
    public static Activity Create(
        Guid id,
        string entityType,
        Guid entityId,
        string type,
        Guid assignedToUserId,
        DateTimeOffset dueAt,
        IActivityRegistry registry,
        Guid? createdByUserId = null,
        string? description = null,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(registry);

        if (entityId == Guid.Empty)
        {
            throw new ArgumentException("EntityId cannot be Guid.Empty.", nameof(entityId));
        }
        if (assignedToUserId == Guid.Empty)
        {
            throw new ArgumentException("AssignedToUserId cannot be Guid.Empty.", nameof(assignedToUserId));
        }
        if (!registry.TryGet(type, out _))
        {
            throw new ArgumentException(
                $"Activity type '{type}' is not registered. Either the contributing module is not loaded, or the type name is misspelled.",
                nameof(type));
        }

        Activity activity = new()
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Type = type,
            AssignedToUserId = assignedToUserId,
            DueAt = dueAt,
            CreatedByUserId = createdByUserId,
            Description = description,
            Status = ActivityStatus.Open,
            TenantId = tenantId,
        };

        activity.AddDomainEvent(new ActivityAssignedEvent(id, type, assignedToUserId, dueAt, entityType, entityId));
        activity.AddDistributedEvent(new ActivityAssignedEto(id, type, assignedToUserId, dueAt, entityType, entityId, tenantId));

        return activity;
    }

    /// <summary>Polymorphic FK target — wire identifier of the host entity (e.g. <c>"Granit.Parties.Party"</c>).</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Polymorphic FK target — primary key of the host entity row.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Activity type name — must resolve in <see cref="IActivityRegistry"/>.</summary>
    public string Type { get; private set; } = string.Empty;

    /// <summary>The user expected to perform the activity.</summary>
    public Guid AssignedToUserId { get; private set; }

    /// <summary>Optional creator id (system-generated activities leave this null).</summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>When the activity should be performed by.</summary>
    public DateTimeOffset DueAt { get; private set; }

    /// <summary>Optional free-text description from the creator.</summary>
    public string? Description { get; private set; }

    /// <summary>Lifecycle status — see <see cref="ActivityStatus"/>.</summary>
    public ActivityStatus Status { get; private set; }

    /// <summary>Set when the activity transitions to <see cref="ActivityStatus.Done"/> or <see cref="ActivityStatus.Cancelled"/>.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>The user who completed or cancelled the activity.</summary>
    public Guid? CompletedByUserId { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Marks the activity as <see cref="ActivityStatus.Done"/>. Idempotent —
    /// completing an already-completed activity throws (use <see cref="Cancel"/>
    /// for un-do; activities are append-only).
    /// </summary>
    public void Complete(Guid userId, DateTimeOffset at)
    {
        EnsureOpen(nameof(Complete));
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("CompletedBy user id cannot be Guid.Empty.", nameof(userId));
        }

        Status = ActivityStatus.Done;
        CompletedAt = at;
        CompletedByUserId = userId;

        AddDomainEvent(new ActivityCompletedEvent(Id, userId, at));
        AddDistributedEvent(new ActivityCompletedEto(Id, userId, at, TenantId));
    }

    /// <summary>
    /// Marks the activity as <see cref="ActivityStatus.Cancelled"/>. Throws if
    /// the activity is already terminal — same append-only rule as
    /// <see cref="Complete"/>.
    /// </summary>
    public void Cancel(Guid userId, DateTimeOffset at)
    {
        EnsureOpen(nameof(Cancel));
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("CancelledBy user id cannot be Guid.Empty.", nameof(userId));
        }

        Status = ActivityStatus.Cancelled;
        CompletedAt = at;
        CompletedByUserId = userId;

        AddDomainEvent(new ActivityCancelledEvent(Id, userId, at));
    }

    /// <summary>
    /// Reassigns the activity to a different user. Allowed only while
    /// <see cref="ActivityStatus.Open"/>.
    /// </summary>
    public void Reassign(Guid newAssigneeUserId)
    {
        EnsureOpen(nameof(Reassign));
        if (newAssigneeUserId == Guid.Empty)
        {
            throw new ArgumentException("New assignee id cannot be Guid.Empty.", nameof(newAssigneeUserId));
        }

        if (newAssigneeUserId == AssignedToUserId)
        {
            return; // no-op — same assignee
        }

        Guid previous = AssignedToUserId;
        AssignedToUserId = newAssigneeUserId;
        AddDomainEvent(new ActivityReassignedEvent(Id, previous, newAssigneeUserId));
    }

    /// <summary>
    /// Updates <see cref="DueAt"/>. Allowed only while
    /// <see cref="ActivityStatus.Open"/>.
    /// </summary>
    public void Reschedule(DateTimeOffset newDueAt)
    {
        EnsureOpen(nameof(Reschedule));
        if (newDueAt == DueAt)
        {
            return; // no-op — same due date
        }

        DateTimeOffset previous = DueAt;
        DueAt = newDueAt;
        AddDomainEvent(new ActivityRescheduledEvent(Id, previous, newDueAt));
    }

    private void EnsureOpen(string operation)
    {
        if (Status != ActivityStatus.Open)
        {
            throw new InvalidOperationException(
                $"Activity {Id} is in {Status} state and cannot accept the '{operation}' operation. Activities are append-only — create a new one if a follow-up is needed.");
        }
    }
}
