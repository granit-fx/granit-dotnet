namespace Granit.Identity.Models;

/// <summary>
/// Data required to create a new user in the identity provider.
/// </summary>
/// <param name="Username">Login name (required).</param>
/// <param name="Email">Email address (required).</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Enabled">Whether the account should be active immediately.</param>
/// <param name="TemporaryPassword">
/// Optional temporary password. When set, the user will be prompted to change it at first login.
/// </param>
public sealed record IdentityUserCreate(
    string Username,
    string Email,
    string? FirstName = null,
    string? LastName = null,
    bool Enabled = true,
    string? TemporaryPassword = null);
