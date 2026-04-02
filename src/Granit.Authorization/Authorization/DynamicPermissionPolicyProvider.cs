using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Granit.Authorization.Authorization;

/// <summary>
/// Replaces the default ASP.NET Core <see cref="IAuthorizationPolicyProvider"/> to create
/// authorization policies on the fly for any registered permission name.
/// Falls back to <see cref="DefaultAuthorizationPolicyProvider"/> for standard policies
/// such as "Authenticated" or "Admin".
/// </summary>
internal sealed class DynamicPermissionPolicyProvider(
    IOptions<AuthorizationOptions> options,
    IPermissionDefinitionManager definitionManager) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    /// <inheritdoc />
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    /// <inheritdoc />
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
        definitionManager.Exists(policyName)
            ? Task.FromResult<AuthorizationPolicy?>(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(policyName))
                    .Build())
            : _fallback.GetPolicyAsync(policyName);
}
