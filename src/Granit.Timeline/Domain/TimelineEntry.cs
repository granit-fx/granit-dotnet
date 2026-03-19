using Granit.Core.Domain;
using Granit.Timeline.Events;

namespace Granit.Timeline.Domain;

/// <summary>
/// A single entry in the activity stream for any <see cref="ITimelined"/> entity.
/// Three entry types coexist in the same table:
/// <list type="bullet">
///   <item><see cref="TimelineEntryType.Comment"/> — human-authored, soft-deletable (RGPD).</item>
///   <item><see cref="TimelineEntryType.InternalNote"/> — human-authored, staff-only, soft-deletable.</item>
///   <item><see cref="TimelineEntryType.SystemLog"/> — auto-generated, INSERT-only immutable (ISO 27001).</item>
/// </list>
/// </summary>
public sealed class TimelineEntry : CreationAuditedAggregateRoot, ISoftDeletable, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private TimelineEntry() { }

    /// <summary>
    /// Creates a new <see cref="TimelineEntry"/>.
    /// </summary>
    public static TimelineEntry Create(
        Guid id,
        string entityType,
        string entityId,
        TimelineEntryType entryType,
        string body,
        string authorId,
        string authorName,
        DateTimeOffset createdAt,
        string createdBy,
        Guid? tenantId = null,
        Guid? parentEntryId = null) => new()
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            EntryType = entryType,
            Body = body,
            AuthorId = authorId,
            AuthorName = authorName,
            ParentEntryId = parentEntryId,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            TenantId = tenantId,
        };

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
    /// Raises a <see cref="TimelineEntryPosted"/> domain event.
    /// Called by the store after the entry is fully initialized.
    /// </summary>
    internal void RaisePostedEvent() =>
        AddDomainEvent(new TimelineEntryPosted(Id, EntityType, EntityId, EntryType, AuthorId));

    /// <summary>
    /// Marks this entry as soft-deleted and raises a <see cref="TimelineEntrySoftDeleted"/> domain event.
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
        AddDomainEvent(new TimelineEntrySoftDeleted(Id, EntityType, EntityId));
    }
}
