namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>Request DTO for admin user creation.</summary>
/// <param name="Email">The user's email address.</param>
/// <param name="FirstName">Optional first name.</param>
/// <param name="LastName">Optional last name.</param>
/// <param name="TemporaryPassword">Optional temporary password (forces change on next login).</param>
#pragma warning disable GRSEC003 // DTO property name, not a secret
public sealed record AdminUserCreateRequest(
    string Email,
    string? FirstName,
    string? LastName,
    string? TemporaryPassword);
#pragma warning restore GRSEC003
