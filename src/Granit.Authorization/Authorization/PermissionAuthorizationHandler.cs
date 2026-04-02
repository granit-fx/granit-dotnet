using Microsoft.AspNetCore.Authorization;

namespace Granit.Authorization.Authorization;

internal sealed class PermissionAuthorizationHandler(IPermissionChecker permissionChecker)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (await permissionChecker.IsGrantedAsync(requirement.PermissionName).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
