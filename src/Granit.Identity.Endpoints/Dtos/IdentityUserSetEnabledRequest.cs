namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Request body for enabling or disabling a user in the identity provider.
/// </summary>
public sealed record IdentityUserSetEnabledRequest(bool Enabled);
