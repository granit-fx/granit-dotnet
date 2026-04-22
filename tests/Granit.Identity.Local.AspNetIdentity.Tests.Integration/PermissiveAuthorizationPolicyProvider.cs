using Microsoft.AspNetCore.Authorization;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Integration;

/// <summary>
/// Policy provider that resolves every named policy — including the dynamic
/// <c>IdentityLocal.Roles.*</c> permission names declared by the role endpoints —
/// to a single "requires an authenticated user" policy. This short-circuits the
/// real <c>DynamicPermissionPolicyProvider</c> / <c>PermissionChecker</c> path
/// so the endpoint tests aren't pulled into seeding permission grants, which is
/// orthogonal to the visibility-matrix behaviour they're meant to verify.
/// </summary>
internal sealed class PermissiveAuthorizationPolicyProvider : IAuthorizationPolicyProvider
{
    private static readonly AuthorizationPolicy AuthenticatedPolicy =
        new AuthorizationPolicyBuilder(TestAuthHandler.SchemeName)
            .RequireAuthenticatedUser()
            .Build();

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        Task.FromResult(AuthenticatedPolicy);

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        Task.FromResult<AuthorizationPolicy?>(AuthenticatedPolicy);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        Task.FromResult<AuthorizationPolicy?>(AuthenticatedPolicy);
}
