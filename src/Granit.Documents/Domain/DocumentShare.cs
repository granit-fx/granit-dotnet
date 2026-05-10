using Granit.Documents.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Documents.Domain;

/// <summary>
/// Aggregate root representing an ACL grant on a folder or a document, conferring a
/// <see cref="SharePermissionLevel"/> level to a user, role, or group, optionally with an expiration.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR-052, exactly one of <see cref="FolderId"/> or <see cref="DocumentId"/> is non-null
/// — enforced both at the C# factory level (<see cref="ShareToFolder"/> / <see cref="ShareToDocument"/>)
/// and at the database level (CHECK <c>ck_share_target_exactly_one</c>).
/// </para>
/// <para>
/// Folder shares with <see cref="IsDefault"/> = <c>true</c> propagate to descendants via the
/// path-based effective-permission resolver introduced by F6.4 — F6.1 only persists the flag.
/// Document shares ignore <see cref="IsDefault"/>; the factories normalise it accordingly.
/// </para>
/// </remarks>
public sealed class DocumentShare : AggregateRoot, IMultiTenant
{
    /// <summary>Parameterless constructor required by the EF Core materialiser.</summary>
    private DocumentShare() { }

    /// <summary>
    /// Creates a share that grants <paramref name="permission"/> to (<paramref name="granteeType"/>,
    /// <paramref name="granteeId"/>) on the folder identified by <paramref name="folderId"/>.
    /// </summary>
    /// <param name="id">Unique identifier of the new share.</param>
    /// <param name="tenantId">Identifier of the tenant the share belongs to.</param>
    /// <param name="folderId">Target folder identifier.</param>
    /// <param name="granteeType">Grantee kind (User / Role / Group).</param>
    /// <param name="granteeId">Identifier of the grantee.</param>
    /// <param name="permission">Permission level conferred.</param>
    /// <param name="isDefault">When <c>true</c> the grant inherits to descendants via path-prefix matching.</param>
    /// <param name="createdByUserId">Identifier of the user creating the share (audit attribution).</param>
    /// <param name="createdAt">Creation timestamp.</param>
    /// <param name="expiresAt">Optional expiration (UTC). Past expirations are rejected.</param>
    public static DocumentShare ShareToFolder(
        Guid id,
        Guid? tenantId,
        Guid folderId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        bool isDefault,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null)
    {
        if (folderId == Guid.Empty)
        {
            throw new ArgumentException("FolderId cannot be empty.", nameof(folderId));
        }
        return Create(
            id, tenantId, ShareTargetType.Folder, folderId: folderId, documentId: null,
            granteeType, granteeId, permission, isDefault,
            createdByUserId, createdAt, expiresAt);
    }

    /// <summary>
    /// Creates a share that grants <paramref name="permission"/> to (<paramref name="granteeType"/>,
    /// <paramref name="granteeId"/>) on the document identified by <paramref name="documentId"/>.
    /// </summary>
    /// <remarks>
    /// Document shares never inherit; <see cref="IsDefault"/> is always stored as <c>false</c>.
    /// </remarks>
    public static DocumentShare ShareToDocument(
        Guid id,
        Guid? tenantId,
        Guid documentId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("DocumentId cannot be empty.", nameof(documentId));
        }
        return Create(
            id, tenantId, ShareTargetType.Document, folderId: null, documentId: documentId,
            granteeType, granteeId, permission, isDefault: false,
            createdByUserId, createdAt, expiresAt);
    }

    /// <summary>
    /// Canonical aggregate factory. Use the type-safe wrappers <see cref="ShareToFolder"/>
    /// and <see cref="ShareToDocument"/> from application code; this overload exists primarily
    /// to satisfy the <c>Create(...)</c> factory convention enforced by the architecture
    /// tests and to centralise the invariant checks shared between the two wrappers.
    /// </summary>
    public static DocumentShare Create(
        Guid id,
        Guid? tenantId,
        ShareTargetType targetType,
        Guid? folderId,
        Guid? documentId,
        ShareGranteeType granteeType,
        Guid granteeId,
        SharePermissionLevel permission,
        bool isDefault,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null)
    {
        ValidateTarget(targetType, folderId, documentId);
        ValidateGrantee(granteeId);
        ValidatePermission(permission);
        ValidateGranteeType(granteeType);
        ValidateExpiration(expiresAt, createdAt);

        DocumentShare share = new()
        {
            Id = id,
            TenantId = tenantId,
            TargetType = targetType,
            FolderId = folderId,
            DocumentId = documentId,
            GranteeType = granteeType,
            GranteeId = granteeId,
            Permission = permission,
            // Document shares never inherit — normalise here so the wrapper signatures stay
            // honest even if a caller bypasses them and reaches Create directly.
            IsDefault = targetType == ShareTargetType.Folder && isDefault,
            CreatedByUserId = createdByUserId,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
        share.AddDomainEvent(new DocumentShareGrantedEvent(
            share.Id, share.TenantId, share.TargetType, share.FolderId, share.DocumentId,
            share.GranteeType, share.GranteeId, share.Permission, share.IsDefault, share.ExpiresAt));
        return share;
    }

    private static void ValidateTarget(
        ShareTargetType targetType,
        Guid? folderId,
        Guid? documentId)
    {
        if (!Enum.IsDefined(targetType))
        {
            throw new ArgumentException(
                $"Unknown share target type '{targetType}'.", nameof(targetType));
        }
        bool folderProvided = folderId is { } fid && fid != Guid.Empty;
        bool documentProvided = documentId is { } did && did != Guid.Empty;
        if (targetType == ShareTargetType.Folder && (!folderProvided || documentProvided))
        {
            throw new ArgumentException(
                "Folder shares require a non-empty folderId and no documentId.", nameof(folderId));
        }
        if (targetType == ShareTargetType.Document && (!documentProvided || folderProvided))
        {
            throw new ArgumentException(
                "Document shares require a non-empty documentId and no folderId.", nameof(documentId));
        }
    }

    /// <summary>Identifier of the tenant that owns this share.</summary>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    /// <remarks>Explicit implementation preserves <c>private set</c> on the public property.</remarks>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Whether this grant is on a folder or a document.</summary>
    public ShareTargetType TargetType { get; private set; }

    /// <summary>Target folder identifier — non-null exactly when <see cref="TargetType"/> is <see cref="ShareTargetType.Folder"/>.</summary>
    public Guid? FolderId { get; private set; }

    /// <summary>Target document identifier — non-null exactly when <see cref="TargetType"/> is <see cref="ShareTargetType.Document"/>.</summary>
    public Guid? DocumentId { get; private set; }

    /// <summary>Kind of principal the grant targets.</summary>
    public ShareGranteeType GranteeType { get; private set; }

    /// <summary>Identifier of the grantee (user, role, or group depending on <see cref="GranteeType"/>).</summary>
    public Guid GranteeId { get; private set; }

    /// <summary>Permission level conferred.</summary>
    public SharePermissionLevel Permission { get; private set; }

    /// <summary>
    /// For folder shares: when <c>true</c>, the grant inherits to descendants via path-prefix
    /// matching (semantics introduced by F6.4). Always <c>false</c> for document shares.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>Optional expiration timestamp. <c>null</c> means the grant never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Identifier of the user who granted the share (audit attribution).</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// Returns <c>true</c> when the share has expired relative to <paramref name="now"/>.
    /// </summary>
    public bool IsExpired(DateTimeOffset now) =>
        ExpiresAt is { } expiresAt && expiresAt <= now;

    /// <summary>
    /// Marks the share as revoked by emitting <see cref="DocumentShareRevokedEvent"/>. The
    /// service layer is responsible for the actual delete; this method exists so revocation
    /// goes through the aggregate and the event is dispatched alongside the SaveChanges.
    /// </summary>
    public void Revoke() =>
        AddDomainEvent(new DocumentShareRevokedEvent(
            Id, TenantId, TargetType, FolderId, DocumentId, GranteeType, GranteeId));

    private static void ValidateGrantee(Guid granteeId)
    {
        if (granteeId == Guid.Empty)
        {
            throw new ArgumentException("GranteeId cannot be empty.", nameof(granteeId));
        }
    }

    private static void ValidatePermission(SharePermissionLevel permission)
    {
        if (!Enum.IsDefined(permission))
        {
            throw new ArgumentException(
                $"Unknown share permission '{permission}'.", nameof(permission));
        }
    }

    private static void ValidateGranteeType(ShareGranteeType granteeType)
    {
        if (!Enum.IsDefined(granteeType))
        {
            throw new ArgumentException(
                $"Unknown grantee type '{granteeType}'.", nameof(granteeType));
        }
    }

    private static void ValidateExpiration(DateTimeOffset? expiresAt, DateTimeOffset createdAt)
    {
        if (expiresAt is { } v && v <= createdAt)
        {
            throw new ArgumentException(
                "ExpiresAt must be strictly greater than the creation time.", nameof(expiresAt));
        }
    }
}
