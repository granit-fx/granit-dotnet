namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response body for the headless login endpoint.
/// </summary>
/// <param name="Succeeded">Whether authentication succeeded.</param>
/// <param name="RequiresTwoFactor">Whether two-factor authentication is required.</param>
/// <param name="IsLockedOut">Whether the account is locked out.</param>
/// <param name="IsNotAllowed">Whether sign-in is not allowed (email not confirmed, etc.).</param>
public sealed record AccountLoginResponse(
    bool Succeeded,
    bool RequiresTwoFactor = false,
    bool IsLockedOut = false,
    bool IsNotAllowed = false);
