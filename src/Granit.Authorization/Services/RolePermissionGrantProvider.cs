namespace Granit.Authorization.Services;

/// <summary>
/// Grants evaluated against the role claims of the current principal. Second in the
/// evaluation order (after user, before client): a role-level grant is more specific than
/// a client-level grant but less specific than an explicit user grant.
/// </summary>
internal sealed class RolePermissionGrantProvider : IPermissionGrantProvider
{
    /// <inheritdoc />
    public string Name => PermissionGrantProviderNames.Role;

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderKeys(PermissionGrantLookupContext context) =>
        context.Roles;
}
