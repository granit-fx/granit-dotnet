namespace Granit.Authorization.Services;

/// <summary>
/// Grants evaluated against the <c>sub</c> (user id) of the current principal. Evaluated
/// first because a user-specific grant is more specific than any role- or client-level grant.
/// </summary>
internal sealed class UserPermissionGrantProvider : IPermissionGrantProvider
{
    /// <inheritdoc />
    public string Name => PermissionGrantProviderNames.User;

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderKeys(PermissionGrantLookupContext context) =>
        string.IsNullOrEmpty(context.UserId) ? [] : [context.UserId];
}
