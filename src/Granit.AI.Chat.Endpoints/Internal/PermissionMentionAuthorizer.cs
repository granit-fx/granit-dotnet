using Granit.AI.Chat.Mentions;
using Granit.Authorization;

namespace Granit.AI.Chat.Endpoints.Internal;

/// <summary>
/// Permission-bound <see cref="IAIMentionAuthorizer"/>: a resolver is allowed when it declares no
/// <see cref="IAIMentionResolver.RequiredPermission"/>, or when the caller is granted it. Replaces
/// the base permissive default so a type like <c>@user</c> (gated on <c>Identity.Users.Read</c>) is
/// hidden from callers who may chat but may not read that directory (ADR-067, ISO 27001 A.9.4).
/// </summary>
internal sealed class PermissionMentionAuthorizer(IPermissionChecker permissionChecker) : IAIMentionAuthorizer
{
    public async ValueTask<bool> IsAuthorizedAsync(
        IAIMentionResolver resolver, CancellationToken cancellationToken = default)
    {
        string? permission = resolver.RequiredPermission;
        return permission is null
            || await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false);
    }
}
