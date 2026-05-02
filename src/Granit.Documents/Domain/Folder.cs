using Granit.Documents.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Domain;

/// <summary>
/// Aggregate root representing a folder in a tenant's document tree.
/// </summary>
/// <remarks>
/// <para>
/// Each tenant has exactly one invisible <b>tenant root</b> folder
/// (<see cref="IsTenantRoot"/> = <c>true</c>, <see cref="ParentFolderId"/> = <c>null</c>,
/// <see cref="Path"/> = <c>"/"</c>). All user-created folders descend from it; documents
/// reference a folder (root included). The root unifies SQL: there is no
/// <c>FolderId IS NULL</c> branch to handle, and tenant-wide permission grants are simply
/// shares on the root folder.
/// </para>
/// <para>
/// The root cannot be renamed, moved, trashed, or deleted (invariants enforced at the
/// aggregate level). Bootstrap is handled by <c>IDocumentBootstrapService</c> in F2.2.
/// </para>
/// <para>
/// The <see cref="Path"/> column is materialised so list/search and ACL resolution can
/// run as a single non-recursive SQL query (see ADR-052 §Permission resolution model).
/// </para>
/// </remarks>
public sealed class Folder : AggregateRoot, IMultiTenant
{
    /// <summary>Path separator used in the materialised <see cref="Path"/>.</summary>
    public const string PathSeparator = "/";

    /// <summary>The materialised <see cref="Path"/> of every tenant root.</summary>
    public const string TenantRootPath = "/";

    /// <summary>Maximum length, in characters, of a folder <see cref="Name"/>.</summary>
    public const int MaxNameLength = 255;

    /// <summary>Maximum length, in characters, of the materialised <see cref="Path"/>.</summary>
    public const int MaxPathLength = 1024;

    /// <summary>Parameterless constructor required by the EF Core materialiser.</summary>
    private Folder() { }

    /// <summary>
    /// Creates a non-root folder under <paramref name="parent"/>.
    /// </summary>
    /// <param name="id">Unique identifier of the new folder.</param>
    /// <param name="parent">The parent folder (the tenant root or any non-trashed folder).</param>
    /// <param name="name">User-facing name (validated).</param>
    /// <param name="ownerUserId">Identifier of the user who owns the folder.</param>
    public static Folder Create(Guid id, Folder parent, string name, Guid ownerUserId)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ValidateName(name);
        if (parent.Status != FolderStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot create a folder under a non-active parent (parent {parent.Id} is {parent.Status}).");
        }

        string path = ComputeChildPath(parent.Path, name);
        if (path.Length > MaxPathLength)
        {
            throw new ArgumentException(
                $"Resulting folder path exceeds {MaxPathLength} characters.", nameof(name));
        }

        Folder folder = new()
        {
            Id = id,
            TenantId = parent.TenantId,
            ParentFolderId = parent.Id,
            Name = name,
            Path = path,
            Depth = parent.Depth + 1,
            OwnerUserId = ownerUserId,
            IsTenantRoot = false,
            Status = FolderStatus.Active,
        };
        folder.AddDomainEvent(new FolderCreatedEvent(
            folder.Id, folder.TenantId, folder.ParentFolderId, folder.Name, folder.Path, IsTenantRoot: false));
        return folder;
    }

    /// <summary>
    /// Creates the unique tenant root folder. Internal: only called by the bootstrap service
    /// (F2.2 <c>IDocumentBootstrapService.EnsureTenantRootAsync</c>).
    /// </summary>
    /// <param name="id">Unique identifier of the root.</param>
    /// <param name="tenantId">Tenant the root belongs to.</param>
    /// <param name="ownerUserId">Identifier of the user (or system principal) creating the tenant.</param>
    internal static Folder CreateTenantRoot(Guid id, Guid? tenantId, Guid ownerUserId)
    {
        Folder folder = new()
        {
            Id = id,
            TenantId = tenantId,
            ParentFolderId = null,
            Name = string.Empty,
            Path = TenantRootPath,
            Depth = 0,
            OwnerUserId = ownerUserId,
            IsTenantRoot = true,
            Status = FolderStatus.Active,
        };
        folder.AddDomainEvent(new FolderCreatedEvent(
            folder.Id, folder.TenantId, ParentFolderId: null, folder.Name, folder.Path, IsTenantRoot: true));
        return folder;
    }

    /// <summary>Identifier of the tenant that owns this folder.</summary>
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

    /// <summary>Parent folder identifier. <c>null</c> only when <see cref="IsTenantRoot"/> is true.</summary>
    public Guid? ParentFolderId { get; private set; }

    /// <summary>User-facing name of the folder.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Materialised path: <c>"/"</c> for the root, <c>"/A/B/C"</c> otherwise.</summary>
    public string Path { get; private set; } = TenantRootPath;

    /// <summary>Depth in the tenant tree. <c>0</c> for the root, <c>1</c> for direct children, etc.</summary>
    public int Depth { get; private set; }

    /// <summary>Identifier of the user who owns the folder.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>
    /// <c>true</c> for the unique invisible root of the tenant. Exactly one row per tenant
    /// satisfies this (enforced by a partial unique index in
    /// <c>FolderConfiguration</c>).
    /// </summary>
    public bool IsTenantRoot { get; private set; }

    /// <summary>Lifecycle status. Trashed folders are eligible for permanent deletion via F8 / F9.2.</summary>
    public FolderStatus Status { get; private set; }

    /// <summary>UTC instant when the folder was trashed; <c>null</c> while <see cref="Status"/> is <see cref="FolderStatus.Active"/>.</summary>
    public DateTimeOffset? TrashedAt { get; private set; }

    /// <summary>
    /// Renames the folder and recomputes its materialised <see cref="Path"/>. Descendants'
    /// paths are NOT touched here — the service layer does that in a single SQL UPDATE
    /// during the same transaction (F2.4 introduces the helper).
    /// </summary>
    /// <exception cref="InvalidOperationException">When this folder is the tenant root or trashed.</exception>
    /// <exception cref="ArgumentException">When <paramref name="newName"/> is invalid.</exception>
    public void Rename(string newName)
    {
        ThrowIfTenantRoot(nameof(Rename));
        ThrowIfTrashed(nameof(Rename));
        ValidateName(newName);

        if (string.Equals(Name, newName, StringComparison.Ordinal))
        {
            return;
        }

        string oldName = Name;
        string oldPath = Path;
        string parentPath = oldPath[..^(oldName.Length + PathSeparator.Length)];
        // parentPath is the slice up to (and excluding) the trailing "/<oldName>"; for direct
        // children of the root, parentPath collapses to the empty string, which we map back to "/".
        if (parentPath.Length == 0)
        {
            parentPath = TenantRootPath;
        }

        string newPath = ComputeChildPath(parentPath, newName);
        if (newPath.Length > MaxPathLength)
        {
            throw new ArgumentException(
                $"Resulting folder path exceeds {MaxPathLength} characters.", nameof(newName));
        }

        Name = newName;
        Path = newPath;

        AddDomainEvent(new FolderRenamedEvent(Id, oldName, newName));
        AddDomainEvent(new FolderPathChangedEvent(Id, TenantId, oldPath, newPath));
    }

    /// <summary>
    /// Moves the folder under <paramref name="newParent"/>. Updates this folder's
    /// <see cref="Path"/> and <see cref="Depth"/>; descendants are repointed by the service
    /// layer in the same transaction (F2.4).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When this folder is the tenant root, when this folder is trashed, when the target
    /// parent is in a different tenant, when the target is this folder itself, when the
    /// target is a descendant of this folder, or when the target is trashed.
    /// </exception>
    public void MoveTo(Folder newParent)
    {
        ArgumentNullException.ThrowIfNull(newParent);
        ThrowIfTenantRoot(nameof(MoveTo));
        ThrowIfTrashed(nameof(MoveTo));

        if (newParent.Id == Id)
        {
            throw new InvalidOperationException("A folder cannot be moved under itself.");
        }
        if (newParent.TenantId != TenantId)
        {
            throw new InvalidOperationException("Cannot move a folder to a different tenant.");
        }
        if (newParent.Status != FolderStatus.Active)
        {
            throw new InvalidOperationException(
                $"Cannot move under a non-active parent (parent {newParent.Id} is {newParent.Status}).");
        }
        if (IsAncestorOf(newParent))
        {
            throw new InvalidOperationException(
                "Cannot move a folder under one of its own descendants.");
        }
        if (newParent.Id == ParentFolderId)
        {
            return;
        }

        Guid? oldParentId = ParentFolderId;
        string oldPath = Path;
        string newPath = ComputeChildPath(newParent.Path, Name);
        if (newPath.Length > MaxPathLength)
        {
            throw new InvalidOperationException(
                $"Resulting folder path exceeds {MaxPathLength} characters.");
        }

        ParentFolderId = newParent.Id;
        Path = newPath;
        Depth = newParent.Depth + 1;

        AddDomainEvent(new FolderMovedEvent(Id, oldParentId, newParent.Id));
        AddDomainEvent(new FolderPathChangedEvent(Id, TenantId, oldPath, newPath));
    }

    /// <summary>
    /// Sends the folder to the trash (soft-delete via domain status). Cascade trashing of
    /// descendants is performed by the service layer (F8.1).
    /// </summary>
    /// <exception cref="InvalidOperationException">When this folder is the tenant root or already trashed.</exception>
    public void Trash(DateTimeOffset trashedAt)
    {
        ThrowIfTenantRoot(nameof(Trash));
        if (Status == FolderStatus.Trashed)
        {
            throw new InvalidOperationException($"Folder {Id} is already trashed.");
        }

        Status = FolderStatus.Trashed;
        TrashedAt = trashedAt;
        AddDomainEvent(new FolderTrashedEvent(Id, TenantId, trashedAt));
    }

    /// <summary>
    /// Restores a trashed folder. Descendants stay trashed unless restored individually.
    /// </summary>
    /// <exception cref="InvalidOperationException">When this folder is the tenant root or already active.</exception>
    public void Restore()
    {
        ThrowIfTenantRoot(nameof(Restore));
        if (Status != FolderStatus.Trashed)
        {
            throw new InvalidOperationException($"Folder {Id} is not trashed.");
        }

        Status = FolderStatus.Active;
        TrashedAt = null;
        AddDomainEvent(new FolderRestoredEvent(Id, TenantId));
    }

    private bool IsAncestorOf(Folder candidate) =>
        // Path-prefix check: ancestor.Path is "/" or ends with no separator; child paths begin
        // with ancestor.Path + "/". The exact-equality check handles the candidate-is-self case
        // already excluded above but kept here for symmetry with the SQL ancestor-match query.
        candidate.Path == Path
        || candidate.Path.StartsWith(Path == TenantRootPath ? TenantRootPath : Path + PathSeparator,
            StringComparison.Ordinal);

    private static string ComputeChildPath(string parentPath, string childName) =>
        parentPath == TenantRootPath
            ? TenantRootPath + childName
            : parentPath + PathSeparator + childName;

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Folder name exceeds {MaxNameLength} characters.", nameof(name));
        }
        if (name.Contains(PathSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Folder name must not contain the path separator '{PathSeparator}'.",
                nameof(name));
        }
    }

    private void ThrowIfTenantRoot(string operation)
    {
        if (IsTenantRoot)
        {
            throw new InvalidOperationException(
                $"Operation '{operation}' is not allowed on the tenant root folder ({Id}).");
        }
    }

    private void ThrowIfTrashed(string operation)
    {
        if (Status == FolderStatus.Trashed)
        {
            throw new InvalidOperationException(
                $"Operation '{operation}' is not allowed on a trashed folder ({Id}).");
        }
    }
}
