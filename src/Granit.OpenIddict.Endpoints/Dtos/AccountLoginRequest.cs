namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Request body for the headless login endpoint.
/// </summary>
public sealed record AccountLoginRequest(string Login, string Password);
