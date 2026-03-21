using System.Security.Claims;

namespace Granit.OpenIddict.Services;

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the callback processing.</returns>
    Task<ProcessCallbackResult> ProcessCallbackAsync(ClaimsPrincipal principal, string provider, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an external login association.
/// </summary>
/// <param name="LoginProvider">The provider name (e.g., "Google", "Microsoft", "GitHub").</param>
/// <param name="ProviderKey">The provider-specific unique identifier.</param>
/// <param name="ProviderDisplayName">A user-friendly display name for the provider.</param>
public sealed record ExternalLoginInfo(string LoginProvider, string ProviderKey, string? ProviderDisplayName);

/// <summary>
/// Result of processing an external login callback.
/// </summary>
/// <param name="UserId">The user identifier that was authenticated or created.</param>
/// <param name="IsNewUser">Whether a new user was created during the callback.</param>
public sealed record ProcessCallbackResult(Guid UserId, bool IsNewUser);
