using Granit.Authorization;
using Granit.Identity;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Workflow.Notifications.Internal;

/// <summary>
/// Resolves workflow approvers by combining <see cref="IPermissionManagerReader"/> (permission → roles)
/// with <see cref="IIdentityProvider"/> (roles → users).
/// </summary>
/// <remarks>
/// <para>
/// Resolution flow:
/// <list type="number">
///   <item>
///     <see cref="IPermissionManagerReader.GetGranteesAsync"/> retrieves role names
///     granted the required permission (from the authorization database).
///   </item>
///   <item>
///     For each role, <see cref="IIdentityProvider.GetRoleMembersAsync"/> returns the members.
///   </item>
///   <item>User IDs are aggregated and deduplicated.</item>
/// </list>
/// </para>
/// </remarks>
internal sealed partial class IdentityApproverResolver(
    IPermissionManagerReader permissionManagerReader,
    IIdentityProvider identityProvider,
    ICurrentTenant currentTenant,
    ILogger<IdentityApproverResolver> logger) : IApproverResolver
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ResolveApproversAsync(
        string requiredPermission,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        IReadOnlyList<string> roles = await permissionManagerReader.GetGranteesAsync(
            PermissionGrantProviderNames.Role, requiredPermission, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (roles.Count == 0)
        {
            LogNoRolesForPermission(requiredPermission, tenantId);
            return [];
        }

        HashSet<string> userIds = [];

        foreach (string role in roles)
        {
            IReadOnlyList<IIdentityUser> members = await identityProvider.GetRoleMembersAsync(
                role, cancellationToken).ConfigureAwait(false);

            foreach (IIdentityUser user in members)
            {
                userIds.Add(user.UserId);
            }
        }

        LogApproversResolved(userIds.Count, requiredPermission, roles.Count);

        return [.. userIds];
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "No roles found for permission {Permission} (tenant: {TenantId})")]
    private partial void LogNoRolesForPermission(string permission, Guid? tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved {UserCount} approvers for permission {Permission} across {RoleCount} roles")]
    private partial void LogApproversResolved(int userCount, string permission, int roleCount);
}
