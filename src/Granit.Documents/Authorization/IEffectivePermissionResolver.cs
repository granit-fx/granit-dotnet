namespace Granit.Documents.Authorization;

/// <summary>
/// Resolves the effective <see cref="EffectivePermissionLevel"/> a <see cref="DocumentPrincipal"/>
/// holds against a target via the share ACL. Single-target API plus the batch overload that
/// list endpoints use to populate the <c>permission</c> field on each row (F6.5).
/// </summary>
/// <remarks>
/// <para>
/// Implementations must follow the ADR-052 §Permission resolution model: a single non-recursive
/// SQL query that combines a direct-document share with a path-prefix scan over the document's
/// ancestor folders. Highest-permission wins among matching grants (Manage &gt; Edit &gt; Read).
/// Expired grants are filtered. F6.3 layers FusionCache on top of this contract.
/// </para>
/// </remarks>
public interface IEffectivePermissionResolver
{
    /// <summary>
    /// Returns the effective permission the principal holds on the document identified by
    /// <paramref name="documentId"/>. Returns <see cref="EffectivePermissionLevel.None"/>
    /// when the document does not exist (or is excluded by the tenant filter), when the
    /// principal has no grantee ids, or when no matching grant is found.
    /// </summary>
    Task<EffectivePermissionLevel> GetDocumentPermissionAsync(
        Guid documentId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the effective permissions for a batch of documents in a single round-trip.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Used by document list endpoints (F6.5) to populate the <c>permission</c> field on
    /// each row without N+1 queries. The returned dictionary contains an entry for every
    /// id in <paramref name="documentIds"/> — documents that don't exist (or are excluded
    /// by the tenant filter) and documents the principal has no matching grant for resolve
    /// to <see cref="EffectivePermissionLevel.None"/>.
    /// </para>
    /// <para>
    /// The F6.3 cache decorator leverages individual cache entries first and warms only the
    /// missing ones via the inner batch resolution, so a hot page costs zero database
    /// round-trips.
    /// </para>
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetDocumentPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the effective permission the principal holds on the folder identified by
    /// <paramref name="folderId"/>. Resolution scans the folder's own shares plus every
    /// ancestor folder share via the same path-prefix model used for documents (F6.5b).
    /// </summary>
    Task<EffectivePermissionLevel> GetFolderPermissionAsync(
        Guid folderId,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batched counterpart of <see cref="GetFolderPermissionAsync"/> — used by folder list
    /// endpoints to populate the <c>permission</c> field on each row without N+1 queries.
    /// Returns an entry for every requested id; missing rows / no-matching-grant resolve to
    /// <see cref="EffectivePermissionLevel.None"/>.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, EffectivePermissionLevel>> GetFolderPermissionsAsync(
        IReadOnlyCollection<Guid> folderIds,
        DocumentPrincipal principal,
        CancellationToken cancellationToken = default);
}
