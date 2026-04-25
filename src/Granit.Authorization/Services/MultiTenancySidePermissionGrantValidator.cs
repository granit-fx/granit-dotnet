using Granit.Authorization.Domain;
using Granit.MultiTenancy;

namespace Granit.Authorization.Services;

/// <summary>
/// Rejects permission grants whose target scope contradicts either the permission's
/// declared <see cref="MultiTenancySides"/> or — for role grants — the role's declared
/// scope (<see cref="RoleMetadata.MultiTenancySides"/> / <see cref="RoleMetadata.TenantId"/>).
/// </summary>
/// <remarks>
/// <para>
/// Permission checks (always applied):
/// <list type="bullet">
///   <item><see cref="MultiTenancySides.Host"/> permissions must be granted with <c>TenantId == null</c>.</item>
///   <item><see cref="MultiTenancySides.Tenant"/> permissions must be granted with a non-null <c>TenantId</c>.</item>
///   <item><see cref="MultiTenancySides.Both"/> permissions are accepted regardless.</item>
/// </list>
/// </para>
/// <para>
/// Role-side checks (only when <see cref="PermissionGrantValidationContext.ProviderName"/>
/// is <see cref="PermissionGrantProviderNames.Role"/>):
/// <list type="bullet">
///   <item>A <see cref="MultiTenancySides.Host"/> role cannot receive tenant-scoped grants.</item>
///   <item>A <see cref="MultiTenancySides.Tenant"/> role cannot receive host-level grants, and its <see cref="RoleMetadata.TenantId"/> must match the grant's tenant scope.</item>
///   <item>A <see cref="MultiTenancySides.Both"/> role is accepted in either context.</item>
/// </list>
/// The role is located via <see cref="IRoleMetadataStore"/>, trying the grant's tenant
/// scope first and falling back to the global namespace. When no matching role metadata
/// exists the validator returns success — absence of metadata is not treated as a grant
/// violation (it may simply mean the role was registered before the metadata surface
/// existed, or the <c>NullRoleMetadataStore</c> fallback is active).
/// </para>
/// <para>Registered by default via <c>GranitAuthorizationModule</c>.</para>
/// </remarks>
internal sealed class MultiTenancySidePermissionGrantValidator(
    IRoleMetadataStore roleMetadataStore) : IPermissionGrantValidator
{
    /// <inheritdoc />
    public async ValueTask<PermissionGrantValidationResult> ValidateAsync(
        PermissionGrantValidationContext context,
        CancellationToken cancellationToken = default)
    {
        MultiTenancySides permissionSide = context.Definition.MultiTenancySides;

        if (!permissionSide.HasFlag(MultiTenancySides.Host) && context.TenantId is null)
        {
            return PermissionGrantValidationResult.Reject(
                "side_host_forbidden",
                $"Permission '{context.PermissionName}' is Tenant-only; host-level grant (TenantId == null) refused.");
        }

        if (!permissionSide.HasFlag(MultiTenancySides.Tenant) && context.TenantId is not null)
        {
            return PermissionGrantValidationResult.Reject(
                "side_tenant_forbidden",
                $"Permission '{context.PermissionName}' is Host-only; tenant-scoped grant refused.");
        }

        if (context.ProviderName != PermissionGrantProviderNames.Role)
        {
            return PermissionGrantValidationResult.Success;
        }

        RoleMetadata? role = null;
        if (context.TenantId is Guid grantTenantId)
        {
            role = await roleMetadataStore.FindByNameAsync(
                context.ProviderKey, grantTenantId, clientId: null, cancellationToken)
                .ConfigureAwait(false);
        }

        role ??= await roleMetadataStore.FindByNameAsync(
            context.ProviderKey, tenantId: null, clientId: null, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            // No metadata registered for this role — defer to the permission-side checks above.
            // This preserves backward compatibility with deployments that grant to roles created
            // outside the Granit RoleMetadata surface (legacy rows, provider-native roles, tests).
            return PermissionGrantValidationResult.Success;
        }

        if (role.MultiTenancySides == MultiTenancySides.Host && context.TenantId is not null)
        {
            return PermissionGrantValidationResult.Reject(
                "role_side_forbidden",
                $"Role '{context.ProviderKey}' is Host-only; tenant-scoped grant refused.");
        }

        if (role.MultiTenancySides == MultiTenancySides.Tenant)
        {
            if (context.TenantId is null)
            {
                return PermissionGrantValidationResult.Reject(
                    "role_side_forbidden",
                    $"Role '{context.ProviderKey}' is Tenant-only; host-level grant refused.");
            }

            if (role.TenantId != context.TenantId)
            {
                return PermissionGrantValidationResult.Reject(
                    "role_tenant_mismatch",
                    $"Role '{context.ProviderKey}' belongs to tenant {role.TenantId:D}; " +
                    $"grant targeting tenant {context.TenantId:D} refused.");
            }
        }

        return PermissionGrantValidationResult.Success;
    }
}
