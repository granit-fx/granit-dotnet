using Granit.Documents.Domain;

namespace Granit.Documents.Endpoints.Shares.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /folders/{id}/shares</c> and <c>POST /documents/{id}/shares</c>.
/// </summary>
/// <param name="GranteeType">Whether the grant targets a user, a role, or a group.</param>
/// <param name="GranteeId">Identifier of the grantee (user / role / group depending on <paramref name="GranteeType"/>).</param>
/// <param name="Permission">Permission level conferred (<see cref="SharePermissionLevel.Read"/> / <see cref="SharePermissionLevel.Edit"/> / <see cref="SharePermissionLevel.Manage"/>).</param>
/// <param name="IsDefault">
/// When granting on a folder, controls whether the grant inherits to descendants via the
/// path-based resolver (F6.4 — F6.1 only persists the flag). Ignored for document shares.
/// Defaults to <c>true</c> on folder shares so the typical user expectation (a folder share
/// applies to its contents) holds without an explicit flag.
/// </param>
/// <param name="ExpiresAt">Optional expiration timestamp (UTC). <c>null</c> means the grant never expires.</param>
public sealed record GrantShareRequest(
    ShareGranteeType GranteeType,
    Guid GranteeId,
    SharePermissionLevel Permission,
    bool IsDefault = true,
    DateTimeOffset? ExpiresAt = null);
