namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Registered passkey information.</summary>
public sealed record PasskeyInfoResponse(
    Guid Id,
    string? Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Linked external login provider.</summary>
public sealed record ExternalLoginInfoResponse(
    string LoginProvider,
    string ProviderKey,
    string? ProviderDisplayName);

/// <summary>Impersonation result with tokens.</summary>
#pragma warning disable GRSEC003 // Token result properties, not secrets
public sealed record ImpersonationResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn);
#pragma warning restore GRSEC003
