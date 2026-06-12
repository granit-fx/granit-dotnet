using Granit.IpGeolocation;

namespace Granit.Identity.Endpoints.Dtos;

/// <summary>User identity information.</summary>
public sealed record IdentityUserResponse(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Identity provider role.</summary>
public sealed record IdentityRoleResponse(
    string Id,
    string Name,
    string? Description);

/// <summary>Identity provider group.</summary>
public sealed record IdentityGroupResponse(
    string Id,
    string Name,
    string? Path,
    IReadOnlyList<IdentityGroupResponse> SubGroups);

/// <summary>Active user session.</summary>
public sealed record IdentitySessionResponse(
    string SessionId,
    string? IpAddress,
    DateTimeOffset StartedAt,
    DateTimeOffset LastAccess,
    bool RememberMe,
    IReadOnlyList<string> Clients,
    GeoLocation? Location,
    UserSessionRiskLevel? RiskLevel);

/// <summary>Device activity summary.</summary>
public sealed record IdentityDeviceActivityResponse(
    string? IpAddress,
    DateTimeOffset LastAccess,
    string? Device,
    string? OperatingSystem,
    string? OperatingSystemVersion,
    string? Browser,
    bool Mobile,
    bool Current,
    IReadOnlyList<IdentitySessionResponse> Sessions,
    GeoLocation? Location);
