namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request DTO for user registration.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The password (min 8 chars, digit, uppercase, symbol).</param>
/// <param name="FirstName">Optional first name.</param>
/// <param name="LastName">Optional last name.</param>
public sealed record AccountRegisterRequest(
    string Email,
    string Password,
    string? FirstName,
    string? LastName);
