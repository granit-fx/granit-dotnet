using Granit.Domain;
using Granit.Users;
using Microsoft.AspNetCore.Authorization;

namespace Granit.Authorization.Authorization;

/// <summary>
/// Resource-based authorization handler that grants access when the current user's
/// typed identity (<see cref="ICurrentUserService.UserGuid"/>) matches the
/// <see cref="IOwnable.OwnerId"/> of the resource being authorized.
/// </summary>
/// <remarks>
/// Best-effort: when <see cref="ICurrentUserService.UserGuid"/> is <c>null</c>
/// (non-Guid <c>sub</c> claim, machine actor, unauthenticated request), the
/// handler returns silently without succeeding — the requirement remains
/// unsatisfied and a stacked handler (permission-based, role-based) may still
/// grant access. The handler never calls <c>context.Fail()</c>.
/// </remarks>
internal sealed class OwnedByCurrentUserHandler(ICurrentUserService currentUser)
    : AuthorizationHandler<OwnedRequirement, IOwnable>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnedRequirement requirement,
        IOwnable resource)
    {
        if (currentUser.UserGuid is Guid id && id == resource.OwnerId)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
