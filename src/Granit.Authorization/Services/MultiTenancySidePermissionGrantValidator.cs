using Granit.MultiTenancy;

namespace Granit.Authorization.Services;

/// <summary>
/// Rejects permission grants whose target scope contradicts the permission's declared
/// <see cref="MultiTenancySide"/>:
/// <list type="bullet">
/// <item><see cref="MultiTenancySide.Host"/> permissions must be granted with <c>TenantId == null</c>.</item>
/// <item><see cref="MultiTenancySide.Tenant"/> permissions must be granted with a non-null <c>TenantId</c>.</item>
/// <item><see cref="MultiTenancySide.Both"/> permissions are accepted regardless.</item>
/// </list>
/// Registered by default via <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class MultiTenancySidePermissionGrantValidator : IPermissionGrantValidator
{
    /// <inheritdoc />
    public ValueTask<PermissionGrantValidationResult> ValidateAsync(
        PermissionGrantValidationContext context,
        CancellationToken cancellationToken = default)
    {
        MultiTenancySide side = context.Definition.MultiTenancySide;

        if (!side.HasFlag(MultiTenancySide.Host) && context.TenantId is null)
        {
            return ValueTask.FromResult(PermissionGrantValidationResult.Reject(
                "side_host_forbidden",
                $"Permission '{context.PermissionName}' is Tenant-only; host-level grant (TenantId == null) refused."));
        }

        if (!side.HasFlag(MultiTenancySide.Tenant) && context.TenantId is not null)
        {
            return ValueTask.FromResult(PermissionGrantValidationResult.Reject(
                "side_tenant_forbidden",
                $"Permission '{context.PermissionName}' is Host-only; tenant-scoped grant refused."));
        }

        return ValueTask.FromResult(PermissionGrantValidationResult.Success);
    }
}
