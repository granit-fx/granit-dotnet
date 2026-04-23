using Granit.DataProtection;
using Granit.Domain;
using Granit.Timeline.Domain.ValueObjects;
using Granit.Timeline.Events;

namespace Granit.Timeline.Domain;

/// <summary>
/// A single entry in the activity stream for any <see cref="ITimelined"/> entity.
/// Three entry types coexist in the same table:
/// <list type="bullet">
///   <item><see cref="TimelineEntryType.Comment"/> — human-authored, soft-deletable (GDPR).</item>
///   <item><see cref="TimelineEntryType.InternalNote"/> — human-authored, staff-only, soft-deletable.</item>
///   <item><see cref="TimelineEntryType.SystemLog"/> — auto-generated, INSERT-only immutable (ISO 27001).</item>
/// </list>
/// </summary>
public sealed class TimelineEntry : CreationAuditedAggregateRoot, ISoftDeletable, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private TimelineEntry() { }

    /// <summary>Creates a new <see cref="TimelineEntry"/>.</summary>
    /// <param name="id">Unique entry identifier.</param>
    /// <param name="entity">Polymorphic reference to the parent entity.</param>
    /// <param name="entryType">Type of entry (comment, internal note, or system log).</param>
    /// <param name="body">Entry body (Markdown for comments/notes, JSON for system logs).</param>
    /// <param name="author">Denormalized author identity.</param>
    /// <param name="createdAt">Timestamp of creation.</param>
    /// <param name="createdBy">Identifier of the creator (audit trail).</param>
    /// <param name="tenantId">Owning tenant identifier.</param>
    /// <param name="parentEntryId">Parent entry ID for threaded replies.</param>
    public static TimelineEntry Create(
        Guid id,
        EntityReference entity,
        TimelineEntryType entryType,
        string body,
        AuthorInfo author,
        DateTimeOffset createdAt,
        string createdBy,
        Guid? tenantId = null,
        Guid? parentEntryId = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentException.ThrowIfNullOrEmpty(entity.EntityType);
        ArgumentException.ThrowIfNullOrEmpty(entity.EntityId);
        ArgumentException.ThrowIfNullOrEmpty(body);
        ArgumentException.ThrowIfNullOrEmpty(author.Id);
        ArgumentException.ThrowIfNullOrEmpty(createdBy);

        return new()
        {
            Id = id,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            EntryType = entryType,
            Body = body,
            AuthorId = author.Id,
            AuthorName = author.Name,
            ParentEntryId = parentEntryId,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            TenantId = tenantId,
        };
    }

    /// <summary>Entity type name (e.g. "Patient", "Invoice").</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Entity identifier as string (polymorphic reference).</summary>
    public string EntityId { get; private set; } = string.Empty;

    /// <summary>Type of entry (comment, system log, or internal note).</summary>
    public TimelineEntryType EntryType { get; private set; }

    /// <summary>
    /// Entry body. Markdown for <see cref="TimelineEntryType.Comment"/> and
    /// <see cref="TimelineEntryType.InternalNote"/>, structured JSON for
    /// <see cref="TimelineEntryType.SystemLog"/>.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>User ID of the author (denormalized for display performance).</summary>
    public string AuthorId { get; private set; } = string.Empty;

    /// <summary>Display name of the author at the time of posting (denormalized).</summary>
    [SensitiveData]
    public string AuthorName { get; private set; } = string.Empty;

    /// <summary>Optional parent entry ID for threaded replies.</summary>
    public Guid? ParentEntryId { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <inheritdoc/>
    public bool IsDeleted { get; private set; }

    /// <inheritdoc/>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <inheritdoc/>
    public string? DeletedBy { get; private set; }

    /// <inheritdoc/>
    bool ISoftDeletable.IsDeleted
    {
        get => IsDeleted;
        set => IsDeleted = value;
    }

    /// <inheritdoc/>
    DateTimeOffset? ISoftDeletable.DeletedAt
    {
        get => DeletedAt;
        set => DeletedAt = value;
    }

    /// <inheritdoc/>
    string? ISoftDeletable.DeletedBy
    {
        get => DeletedBy;
        set => DeletedBy = value;
    }

    /// <summary>
    /// Raises a <see cref="TimelineEntryPostedEvent"/> domain event.
    /// Called by the store after the entry is fully initialized.
    /// </summary>
    internal void RaisePostedEvent() =>
        AddDomainEvent(new TimelineEntryPostedEvent(Id, EntityType, EntityId, EntryType, AuthorId));

    /// <summary>
    /// Marks this entry as soft-deleted and raises a <see cref="TimelineEntrySoftDeletedEvent"/> domain event.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the entry is a <see cref="TimelineEntryType.SystemLog"/>.</exception>
    internal void SoftDelete(DateTimeOffset deletedAt, string? deletedBy)
    {
        if (EntryType == TimelineEntryType.SystemLog)
        {
            throw new InvalidOperationException("System log entries are immutable and cannot be deleted (ISO 27001 audit trail).");
        }

        IsDeleted = true;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        AddDomainEvent(new TimelineEntrySoftDeletedEvent(Id, EntityType, EntityId));
    }
}
