using System.Security.Claims;
using Granit.Documents.Domain;
using Granit.Documents.Permissions;
using Granit.Taxonomy.Authorization;
using Microsoft.AspNetCore.Http;

namespace Granit.Documents.Endpoints.Authorization;

/// <summary>
/// Tightens <see cref="ITaggablePermissionResolver"/> for the Documents module
/// (T6.1): tag-assignment writes against a <c>Granit.Documents.Domain.Document</c>
/// target require the calling principal to carry the
/// <c>Documents.Documents.Manage</c> permission claim.
/// </summary>
/// <remarks>
/// <para>
/// The resolver is registered as the singleton <see cref="ITaggablePermissionResolver"/>
/// in <c>Granit.Documents.Endpoints</c> via <c>services.Replace(...)</c>; it
/// runs after the base permission check inside the Taxonomy assignment endpoint.
/// </para>
/// <para>
/// For target types other than <see cref="Document"/> the resolver is a
/// pass-through — hosts that taggable additional aggregates remain free to layer
/// their own resolver in front via DI ordering. The proxy endpoints under
/// <c>/api/v1/documents/{id}/tags</c> attach <c>RequireAuthorization</c> claims
/// directly and never reach this resolver.
/// </para>
/// </remarks>
internal sealed class DocumentTaggablePermissionResolver(IHttpContextAccessor http)
    : ITaggablePermissionResolver
{
    private static readonly string DocumentTargetType = typeof(Document).FullName!;

    public Task<bool> IsAuthorizedAsync(
        string targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetType);

        if (!string.Equals(targetType, DocumentTargetType, StringComparison.Ordinal))
        {
            // Not a Documents-owned target — defer to the framework default
            // semantics (allow). Hosts that need to gate other aggregates ship
            // their own resolver.
            return Task.FromResult(true);
        }

        ClaimsPrincipal? user = http.HttpContext?.User;
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult(false);
        }

        bool authorized = user.HasClaim("permission", DocumentsPermissions.Documents.Manage);
        return Task.FromResult(authorized);
    }
}
