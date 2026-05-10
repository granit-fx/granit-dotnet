using Granit.Documents.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Domain;

/// <summary>
/// Aggregate root representing a user-managed document — folder placement, ownership,
/// description, lifecycle status, and the pointer to the currently published version.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-052 phase 1, every document lives in a folder (<see cref="FolderId"/> is
/// non-nullable; the tenant root is the implicit fallback). Document content is stored
/// in <c>Granit.BlobStorage</c>; this aggregate does not hold the bytes.
/// </para>
/// <para>
/// <see cref="CurrentVersionId"/> points at the active <c>DocumentVersion</c> introduced
/// in F4.1; phase 1 of this story (F3.1) leaves it unset for documents created via the
/// scaffolding factory and is populated by <see cref="SetCurrentVersion"/> when the
/// upload flow (F3.2) finalises the first version.
/// </para>
/// <para>
/// <see cref="RowVersion"/> is a manually-incremented optimistic-concurrency token
/// (portable across SQL Server, PostgreSQL, and SQLite). Every behavior method that
/// mutates state increments it; EF Core's <c>IsConcurrencyToken</c> on the column
/// detects concurrent overwrites and surfaces them as <c>DbUpdateConcurrencyException</c>.
/// </para>
/// </remarks>
public sealed class Document : AggregateRoot, IMultiTenant, IEmitEntityLifecycleEvents
{
    /// <summary>Maximum length, in characters, of a document <see cref="Name"/>.</summary>
    public const int MaxNameLength = 255;

    /// <summary>Maximum length, in characters, of a document <see cref="Description"/>.</summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>Parameterless constructor required by the EF Core materialiser.</summary>
    private Document() { }

    /// <summary>
    /// Creates a new document under <paramref name="folder"/>. The first
    /// <c>DocumentVersion</c> is attached separately by the upload-finalize flow (F3.2)
    /// via <see cref="SetCurrentVersion"/>.
    /// </summary>
    /// <param name="id">Unique identifier of the new document.</param>
    /// <param name="folder">Folder the document is created under (must be active and same-tenant).</param>
    /// <param name="ownerUserId">Identifier of the user who owns the document.</param>
    /// <param name="name">User-facing name (validated).</param>
    /// <param name="description">Optional free-text description.</param>
    public static Document Create(
        Guid id,
        Folder folder,
        Guid ownerUserId,
        string name,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ValidateName(name);
        ValidateDescription(description);
        if (folder.Status != FolderStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot create a document under a non-active folder (folder {folder.Id} is {folder.Status}).");
        }

        Document document = new()
        {
            Id = id,
            TenantId = folder.TenantId,
            FolderId = folder.Id,
            OwnerUserId = ownerUserId,
            Name = name,
            Description = description,
            Status = DocumentStatus.Active,
            RowVersion = 1u,
        };
        document.AddDomainEvent(new DocumentCreatedEvent(
            document.Id, document.TenantId, document.FolderId, document.OwnerUserId, document.Name));
        return document;
    }

    /// <summary>Identifier of the tenant that owns this document.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Explicit implementation preserves the <c>private set</c> DDD encapsulation on the
    /// public property while satisfying the interface contract used by
    /// <c>AuditedEntityInterceptor</c> for tenant-id injection.
    /// </remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>The folder the document currently lives in. Never <c>null</c> — every document is in a folder.</summary>
    public Guid FolderId { get; private set; }

    /// <summary>Identifier of the user who owns the document.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>User-facing display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional free-text description (rendered alongside the name in lists / detail views).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Identifier of the active <c>DocumentVersion</c>. <c>null</c> until the first version
    /// has been finalised (F3.2 upload flow) or while a freshly-created document awaits
    /// content.
    /// </summary>
    public Guid? CurrentVersionId { get; private set; }

    /// <summary>Lifecycle status.</summary>
    public DocumentStatus Status { get; private set; }

    /// <summary>UTC instant the document was trashed; <c>null</c> while active.</summary>
    public DateTimeOffset? TrashedAt { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token. Manually incremented on each behavior method;
    /// EF Core's <c>IsConcurrencyToken</c> mapping detects concurrent overwrites.
    /// </summary>
    public uint RowVersion { get; private set; }

    /// <summary>
    /// Renames the document.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="newName"/> is invalid.</exception>
    /// <exception cref="InvalidOperationException">When the document is trashed or permanently deleted.</exception>
    public void Rename(string newName)
    {
        ThrowIfNotActive(nameof(Rename));
        ValidateName(newName);

        if (string.Equals(Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        string oldName = Name;
        Name = newName;
        RowVersion++;
        AddDomainEvent(new DocumentRenamedEvent(Id, oldName, newName));
    }

    /// <summary>Updates the optional description. Pass <c>null</c> to clear it.</summary>
    public void UpdateDescription(string? newDescription)
    {
        ThrowIfNotActive(nameof(UpdateDescription));
        ValidateDescription(newDescription);
        if (string.Equals(Description ?? string.Empty, newDescription ?? string.Empty, StringComparison.Ordinal))
        {
            return;
        }
        Description = newDescription;
        RowVersion++;
    }

    /// <summary>
    /// Moves the document under <paramref name="newFolder"/>. The folder must be active
    /// and same-tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the document is trashed, the target is trashed, or cross-tenant.</exception>
    public void MoveTo(Folder newFolder)
    {
        ArgumentNullException.ThrowIfNull(newFolder);
        ThrowIfNotActive(nameof(MoveTo));
        if (newFolder.Status != FolderStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot move a document under a non-active folder (folder {newFolder.Id} is {newFolder.Status}).");
        }
        if (newFolder.TenantId != TenantId)
        {
            throw new InvalidOperationException("Cannot move a document to a different tenant.");
        }
        if (newFolder.Id == FolderId)
        {
            return;
        }

        Guid oldFolderId = FolderId;
        FolderId = newFolder.Id;
        RowVersion++;
        AddDomainEvent(new DocumentMovedEvent(Id, oldFolderId, newFolder.Id));
    }

    /// <summary>
    /// Sets the document's current version. Called by F3.2's upload-finalise flow
    /// when the first version lands, and by F4 when a new version is uploaded or
    /// an older version is restored as current.
    /// </summary>
    /// <param name="newCurrentVersionId">Identifier of the <c>DocumentVersion</c> to mark as current.</param>
    public void SetCurrentVersion(Guid newCurrentVersionId)
    {
        if (newCurrentVersionId == Guid.Empty)
        {
            throw new ArgumentException("Current version id cannot be empty.", nameof(newCurrentVersionId));
        }
        ThrowIfNotActive(nameof(SetCurrentVersion));

        if (CurrentVersionId == newCurrentVersionId)
        {
            return;
        }

        Guid? oldVersionId = CurrentVersionId;
        CurrentVersionId = newCurrentVersionId;
        RowVersion++;
        AddDomainEvent(new DocumentCurrentVersionChangedEvent(Id, oldVersionId, newCurrentVersionId));
    }

    /// <summary>
    /// Sends the document to the trash (soft-delete via domain status). The empty-trash
    /// background job (F8 / F9.2) promotes it to <see cref="DocumentStatus.PermanentlyDeleted"/>
    /// after the configured retention period.
    /// </summary>
    /// <exception cref="InvalidOperationException">When the document is already trashed or permanently deleted.</exception>
    public void Trash(DateTimeOffset trashedAt)
    {
        if (Status != DocumentStatus.Active)
        {
            throw new InvalidOperationException($"Document {Id} is not active (status: {Status}).");
        }

        Status = DocumentStatus.Trashed;
        TrashedAt = trashedAt;
        RowVersion++;
        AddDomainEvent(new DocumentTrashedEvent(Id, TenantId, trashedAt));
    }

    /// <summary>Restores a trashed document.</summary>
    /// <exception cref="InvalidOperationException">When the document is not trashed.</exception>
    public void Restore()
    {
        if (Status != DocumentStatus.Trashed)
        {
            throw new InvalidOperationException($"Document {Id} is not trashed (status: {Status}).");
        }

        Status = DocumentStatus.Active;
        TrashedAt = null;
        RowVersion++;
        AddDomainEvent(new DocumentRestoredEvent(Id, TenantId));
    }

    /// <summary>
    /// Promotes a trashed document to <see cref="DocumentStatus.PermanentlyDeleted"/>
    /// (F8.2). The aggregate row stays for the GDPR / ISO 27001 audit trail; the bytes
    /// are transitioned to <c>BlobStatus.Deleted</c> by the service layer.
    /// </summary>
    /// <param name="releasedBytes">
    /// Sum of <see cref="DocumentVersion.SizeBytes"/> released by this deletion — used by
    /// the emitted <see cref="DocumentPermanentlyDeletedEvent"/> to drive the F7 quota
    /// decrement downstream consumers may want to react to.
    /// </param>
    /// <param name="deletedAt">UTC instant the deletion is recorded.</param>
    /// <exception cref="InvalidOperationException">
    /// When the document is not currently trashed (only trashed documents can be
    /// permanently deleted; active documents must be trashed first).
    /// </exception>
    public void PermanentlyDelete(long releasedBytes, DateTimeOffset deletedAt)
    {
        if (Status != DocumentStatus.Trashed)
        {
            throw new InvalidOperationException(
                $"Document {Id} cannot be permanently deleted (status: {Status}). Trash it first.");
        }

        Status = DocumentStatus.PermanentlyDeleted;
        RowVersion++;
        AddDomainEvent(new DocumentPermanentlyDeletedEvent(Id, TenantId, deletedAt, releasedBytes));
    }

    private void ThrowIfNotActive(string operation)
    {
        if (Status != DocumentStatus.Active)
        {
            throw new InvalidOperationException(
                $"Operation '{operation}' is not allowed on a document with status {Status} ({Id}).");
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Document name exceeds {MaxNameLength} characters.", nameof(name));
        }
    }

    private static void ValidateDescription(string? description)
    {
        if (description is { Length: > MaxDescriptionLength })
        {
            throw new ArgumentException(
                $"Document description exceeds {MaxDescriptionLength} characters.",
                nameof(description));
        }
    }
}
