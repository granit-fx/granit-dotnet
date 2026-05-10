namespace Granit.Documents.Authorization;

/// <summary>
/// Resolves the effective <see cref="EffectivePermissionLevel"/> a <see cref="DocumentPrincipal"/>
/// holds against a target via the share ACL. Single-target API for F6.2; the batch overload
/// for list endpoints lands with F6.5.
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
}
