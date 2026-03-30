namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for forgot password.
/// </summary>
/// <param name="Email">The user's email address.</param>
public sealed record AccountForgotPasswordRequest(string Email);
