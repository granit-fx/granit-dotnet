using System.Security.Claims;

namespace Granit.Identity.Local.Services;

/// <summary>
/// Manages external login provider associations (Google, Microsoft, GitHub).
/// </summary>
public interface IExternalLoginService
{
    /// <summary>
    /// Gets the external logins linked to a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of linked external login providers.</returns>
    Task<IReadOnlyList<ExternalLoginInfo>> GetLoginsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an external login provider to an existing user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="info">The external login information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddLoginAsync(string userId, ExternalLoginInfo info, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks an external login provider from a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="provider">The provider name (e.g., "Google").</param>
    /// <param name="providerKey">The provider-specific key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveLoginAsync(string userId, string provider, string providerKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes the OAuth callback from an external provider.
    /// </summary>
    /// <param name="principal">The claims principal from the external provider.</param>
    /// <param name="provider">The provider name.</param>
    /// <param name="allowRegistration">
    /// Whether account creation is currently enabled (the <c>AllowSelfRegistration</c> master
    /// switch, resolved per-tenant by the caller). When <see langword="false"/>, an existing
    /// account still authenticates but no new account is created — the callback throws so the
    /// caller can return 403.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the callback processing.</returns>
    Task<ProcessCallbackResult> ProcessCallbackAsync(
        ClaimsPrincipal principal, string provider, bool allowRegistration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an external login association.
/// </summary>
/// <param name="LoginProvider">The provider name (e.g., "Google", "Microsoft", "GitHub").</param>
/// <param name="ProviderKey">The provider-specific unique identifier.</param>
/// <param name="ProviderDisplayName">A user-friendly display name for the provider.</param>
public sealed record ExternalLoginInfo(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

/// <summary>
/// Outcome of processing an external login callback.
/// </summary>
public enum ProcessCallbackStatus
{
    /// <summary>An existing account was authenticated (found by external login or linked by email).</summary>
    ExistingUser,

    /// <summary>A new account was created and authenticated from sufficient provider data.</summary>
    NewUserCreated,

    /// <summary>
    /// The provider returned insufficient data to create an account (e.g. no email). No account
    /// was created; the caller must drive the user through the pre-filled registration flow.
    /// </summary>
    NewUserNeedsProfile,
}

/// <summary>
/// Data carried from an external provider into the pre-filled registration flow when the provider
/// did not return enough to create an account directly. Never includes a credential.
/// </summary>
/// <param name="Provider">The external login provider name.</param>
/// <param name="ProviderKey">The provider-specific unique identifier (sensitive — must not leave the server in plaintext).</param>
/// <param name="Email">The email returned by the provider, if any.</param>
/// <param name="FirstName">The first name returned by the provider, if any.</param>
/// <param name="LastName">The last name returned by the provider, if any.</param>
/// <param name="SuggestedUserName">A suggested username derived from the provider claims, if any.</param>
public sealed record ExternalProfilePrefill(
    string Provider,
    string ProviderKey,
    string? Email,
    string? FirstName,
    string? LastName,
    string? SuggestedUserName);

/// <summary>
/// Result of processing an external login callback.
/// </summary>
/// <param name="Status">The outcome of the callback.</param>
/// <param name="UserId">
/// The authenticated or created user identifier; <see langword="null"/> when
/// <see cref="Status"/> is <see cref="ProcessCallbackStatus.NewUserNeedsProfile"/>.
/// </param>
/// <param name="Prefill">
/// The provider data to seed the registration form; non-null only when <see cref="Status"/> is
/// <see cref="ProcessCallbackStatus.NewUserNeedsProfile"/>.
/// </param>
public sealed record ProcessCallbackResult(
    ProcessCallbackStatus Status,
    Guid? UserId,
    ExternalProfilePrefill? Prefill)
{
    /// <summary>Whether a new account was created during this callback.</summary>
    public bool IsNewUser => Status == ProcessCallbackStatus.NewUserCreated;

    /// <summary>An existing account was authenticated.</summary>
    public static ProcessCallbackResult Existing(Guid userId) => new(ProcessCallbackStatus.ExistingUser, userId, null);

    /// <summary>A new account was created and authenticated.</summary>
    public static ProcessCallbackResult Created(Guid userId) => new(ProcessCallbackStatus.NewUserCreated, userId, null);

    /// <summary>The provider data is insufficient — the pre-filled registration flow must complete creation.</summary>
    public static ProcessCallbackResult NeedsProfile(ExternalProfilePrefill prefill) =>
        new(ProcessCallbackStatus.NewUserNeedsProfile, null, prefill);
}
