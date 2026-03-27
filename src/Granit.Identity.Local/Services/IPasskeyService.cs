namespace Granit.Identity.Local.Services;

/// <summary>
/// Manages WebAuthn/FIDO2 passkey operations.
/// </summary>
/// <remarks>
/// <para>
/// Uses ASP.NET Core Identity's built-in WebAuthn support (.NET 10):
/// <c>UserManager.CreatePasskeyAsync()</c>, <c>UserManager.VerifyPasskeyAsync()</c>.
/// </para>
/// <para>
/// Endpoints return WebAuthn options with <c>mediation: "conditional"</c> for
/// browser-native passkey autofill (Conditional UI).
/// </para>
/// </remarks>
public interface IPasskeyService
{
    /// <summary>
    /// Lists registered passkeys for a user.
    /// </summary>
    Task<IReadOnlyList<PasskeyInfo>> GetPasskeysAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a WebAuthn registration ceremony.
    /// </summary>
    /// <returns>The <c>PublicKeyCredentialCreationOptions</c> JSON for the client.</returns>
    Task<string> BeginRegistrationAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a WebAuthn registration ceremony.
    /// </summary>
    /// <param name="userId">The user registering the passkey.</param>
    /// <param name="credentialJson">The WebAuthn <c>AuthenticatorAttestationResponse</c> JSON.</param>
    /// <param name="name">Optional friendly name for the passkey.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PasskeyInfo> CompleteRegistrationAsync(string userId, string credentialJson, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a WebAuthn assertion ceremony (login).
    /// </summary>
    /// <returns>The <c>PublicKeyCredentialRequestOptions</c> JSON with <c>mediation: "conditional"</c>.</returns>
    Task<string> BeginAssertionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a passkey.
    /// </summary>
    Task RenameAsync(string userId, Guid passkeyId, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a passkey.
    /// </summary>
    /// <remarks>
    /// Returns an error if this is the last credential and the user has no password set.
    /// </remarks>
    Task DeleteAsync(string userId, Guid passkeyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a registered passkey.
/// </summary>
/// <param name="Id">The passkey identifier.</param>
/// <param name="Name">The friendly name.</param>
/// <param name="CreatedAt">When the passkey was registered.</param>
/// <param name="LastUsedAt">When the passkey was last used for authentication.</param>
public sealed record PasskeyInfo(
    Guid Id,
    string? Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);
