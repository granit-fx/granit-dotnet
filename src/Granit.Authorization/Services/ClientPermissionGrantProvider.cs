namespace Granit.Authorization.Services;

/// <summary>
/// Grants evaluated against the OIDC <c>client_id</c> of the current principal. Evaluated
/// last in the default order: client-wide grants are the coarsest level of authorization
/// and should not shadow user- or role-specific grants.
/// </summary>
/// <remarks>
/// Typical use: machine-to-machine callers authenticated via client credentials, where no
/// user or role claims are present but the client itself is trusted for a set of permissions.
/// </remarks>
internal sealed class ClientPermissionGrantProvider : IPermissionGrantProvider
{
    /// <inheritdoc />
    public string Name => PermissionGrantProviderNames.Client;

    /// <inheritdoc />
    public IReadOnlyList<string> GetProviderKeys(PermissionGrantLookupContext context) =>
        string.IsNullOrEmpty(context.ClientId) ? [] : [context.ClientId];
}
