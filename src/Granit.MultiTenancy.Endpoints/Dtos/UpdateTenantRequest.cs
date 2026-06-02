using Granit.Domain;

namespace Granit.MultiTenancy.Endpoints.Dtos;

/// <summary>
/// Request to update an existing tenant's details.
/// </summary>
/// <param name="Name">New display name (max 256 characters).</param>
/// <param name="ContactEmail">New contact email (or <c>null</c> to clear).</param>
/// <param name="Jurisdiction">Privacy regulation code or ISO country code (or <c>null</c> to clear).</param>
/// <param name="ConcurrencyStamp">Stamp from the last read; must match the stored value (prevents lost updates).</param>
public sealed record UpdateTenantRequest(
    string Name,
    string? ContactEmail,
    string? Jurisdiction,
    string ConcurrencyStamp) : IConcurrencyStampRequest;
