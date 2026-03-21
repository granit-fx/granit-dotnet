namespace Granit.OpenIddict.Endpoints.Dtos;

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

/// <summary>
/// Response DTO for user registration.
/// </summary>
/// <param name="UserId">The newly created user's identifier.</param>
/// <param name="RequiresEmailConfirmation">Whether email confirmation is required.</param>
public sealed record AccountRegisterResponse(
    Guid UserId,
    bool RequiresEmailConfirmation);
