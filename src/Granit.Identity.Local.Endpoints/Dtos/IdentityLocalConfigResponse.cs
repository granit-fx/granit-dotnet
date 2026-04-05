namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response exposing the current Identity.Local module configuration.
/// </summary>
public sealed record IdentityLocalConfigResponse(bool AllowSelfRegistration);
