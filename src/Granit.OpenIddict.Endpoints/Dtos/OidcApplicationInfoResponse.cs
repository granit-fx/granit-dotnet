namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Minimal application info returned to authenticated (non-admin) users on the consent page.
/// </summary>
public sealed record OidcApplicationInfoResponse(string? ClientId, string? DisplayName);
