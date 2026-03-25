namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>Request DTO for passkey rename.</summary>
/// <param name="Name">The new friendly name (max 100 chars).</param>
public sealed record PasskeyRenameRequest(string Name);
