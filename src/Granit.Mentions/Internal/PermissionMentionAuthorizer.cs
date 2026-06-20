using Granit.Authorization;

namespace Granit.Mentions.Internal;

/// <summary>
/// Default <see cref="IMentionAuthorizer"/>: a resolver is allowed when it declares no
/// <see cref="IMentionResolver.RequiredPermission"/>, or when the caller is granted it.
/// </summary>
internal sealed class PermissionMentionAuthorizer(IPermissionChecker permissionChecker) : IMentionAuthorizer
{
    public async ValueTask<bool> IsAuthorizedAsync(
        IMentionResolver resolver, CancellationToken cancellationToken = default)
    {
        string? permission = resolver.RequiredPermission;
        return string.IsNullOrWhiteSpace(permission)
            || await permissionChecker.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false);
    }
}
