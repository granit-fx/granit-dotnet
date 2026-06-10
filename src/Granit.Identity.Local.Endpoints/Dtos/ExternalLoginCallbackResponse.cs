namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response DTO for the external login OAuth callback.
/// </summary>
/// <param name="UserId">The user identifier that was authenticated or created.</param>
/// <param name="IsNewUser">Whether a new user was created during the callback.</param>
public sealed record ExternalLoginCallbackResponse(
    Guid UserId,
    bool IsNewUser);
