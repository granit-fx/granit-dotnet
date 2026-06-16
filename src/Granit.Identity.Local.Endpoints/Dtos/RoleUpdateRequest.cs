using Granit.Domain;

namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Request to rename / update a local role's description. Side and tenant scope are immutable.</summary>
/// <param name="Name">New display name (max 256 chars).</param>
/// <param name="ConcurrencyStamp">Stamp from the last read; must match the stored value (prevents lost updates, HTTP 409).</param>
/// <param name="Description">New description (max 2048 chars, nullable to clear).</param>
public sealed record RoleUpdateRequest(
    string Name,
    string ConcurrencyStamp,
    string? Description = null) : IConcurrencyStampRequest;
