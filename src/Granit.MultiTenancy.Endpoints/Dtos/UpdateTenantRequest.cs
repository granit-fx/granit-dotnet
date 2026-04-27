namespace Granit.MultiTenancy.Endpoints.Dtos;

/// <summary>
/// Request to update an existing tenant's details.
/// </summary>
/// <param name="Name">New display name (max 256 characters).</param>
/// <param name="PartyEmail">New contact email (or <c>null</c> to clear).</param>
/// <param name="Jurisdiction">Privacy regulation code or ISO country code (or <c>null</c> to clear).</param>
public sealed record UpdateTenantRequest(
    string Name,
    string? PartyEmail,
    string? Jurisdiction);
