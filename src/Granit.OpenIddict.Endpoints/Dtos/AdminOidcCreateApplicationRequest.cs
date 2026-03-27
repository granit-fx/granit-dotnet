namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request DTO for creating a new OIDC application.
/// </summary>
/// <param name="ClientId">The client identifier (unique).</param>
/// <param name="DisplayName">A human-readable display name.</param>
/// <param name="ClientSecret">The client secret (null for public clients).</param>
/// <param name="Type">The application type (web, native). Default: web.</param>
#pragma warning disable GRSEC003 // ClientSecret is a DTO parameter, not a stored secret
public sealed record AdminOidcCreateApplicationRequest(
    string ClientId,
    string? DisplayName,
    string? ClientSecret,
    string? Type);
#pragma warning restore GRSEC003
