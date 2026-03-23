namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for user group administration endpoints.
/// </summary>
/// <param name="Id">The group's unique identifier.</param>
/// <param name="Name">The group name (unique per tenant).</param>
/// <param name="Description">Human-readable description.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global groups.</param>
public sealed record AdminGroupResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid? TenantId);
