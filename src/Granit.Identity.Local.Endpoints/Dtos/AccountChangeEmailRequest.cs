namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for initiating an email change.
/// </summary>
/// <param name="NewEmail">The desired new email address.</param>
public sealed record AccountChangeEmailRequest(string NewEmail);
