namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for confirming an email change via token.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="NewEmail">The new email address.</param>
/// <param name="Token">The email change confirmation token.</param>
public sealed record AccountConfirmEmailChangeRequest(
    string UserId,
    string NewEmail,
    string Token);
