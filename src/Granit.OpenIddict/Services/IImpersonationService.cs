namespace Granit.OpenIddict.Services;

/// <summary>
/// Manages user impersonation for administrators.
/// </summary>
/// <remarks>
/// <para>
/// Impersonation issues a short-lived token (max 1h) with <c>impersonator_id</c>
/// and <c>impersonator_name</c> claims. The impersonated user receives a transparency
/// notification via <c>Granit.Notifications</c>.
/// </para>
/// <para>
/// Security constraints:
/// <list type="bullet">
/// <item>Impersonated tokens cannot chain-impersonate (no re-impersonation)</item>
/// <item>Token duration: max 1h, hardcoded, non-configurable</item>
/// <item>Mandatory audit log entry on every impersonation</item>
/// <item>Mandatory <see cref="Events.UserImpersonatedEto"/> publication</item>
/// </list>
/// </para>
/// </remarks>
public interface IImpersonationService
{
    /// <summary>
    /// Issues an impersonation token for the specified user.
    /// </summary>
    /// <param name="targetUserId">The user to impersonate.</param>
    /// <param name="impersonatorId">The administrator performing the impersonation.</param>
    /// <param name="impersonatorName">The administrator's display name (for claims).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The impersonation result with access and refresh tokens.</returns>
    Task<ImpersonationResult> ImpersonateAsync(
        string targetUserId,
        string impersonatorId,
        string impersonatorName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends an impersonation session and returns tokens for the original administrator.
    /// </summary>
    /// <param name="impersonatorId">The original administrator's user ID (from the impersonator_id claim).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fresh tokens for the administrator.</returns>
    Task<ImpersonationResult> BackToImpersonatorAsync(
        string impersonatorId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an impersonation operation.
/// </summary>
/// <param name="AccessToken">The issued access token.</param>
/// <param name="RefreshToken">The issued refresh token.</param>
/// <param name="ExpiresIn">Token expiry in seconds.</param>
#pragma warning disable GRSEC003 // Token result properties, not secrets
public sealed record ImpersonationResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
#pragma warning restore GRSEC003
