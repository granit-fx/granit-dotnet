using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to create a new contact.</summary>
/// <param name="Kind">Individual / Company / Department.</param>
/// <param name="Name">Display / legal name (required, max 256 chars).</param>
/// <param name="DefaultCurrency">ISO 4217 alpha-3 currency code (required).</param>
/// <param name="Roles">Initial role flags. Defaults to <see cref="PartyRoles.Customer"/>.</param>
/// <param name="Website">Optional website URL.</param>
/// <param name="Language">Optional ISO locale (e.g. <c>"fr-BE"</c>).</param>
/// <param name="Timezone">Optional IANA timezone (defaults to <c>"UTC"</c>).</param>
/// <param name="TaxId">Optional VAT identifier (e.g. <c>"BE0123456789"</c>).</param>
/// <param name="RegistrationNumber">Optional company registration number.</param>
/// <param name="InternalNotes">Optional admin-only free-form notes (max 8 000 chars). NEVER store PII.</param>
public sealed record PartyCreateRequest(
    PartyKind Kind,
    string Name,
    string DefaultCurrency,
    PartyRoles? Roles = null,
    string? Website = null,
    string? Language = null,
    string? Timezone = null,
    string? TaxId = null,
    string? RegistrationNumber = null,
    string? InternalNotes = null);
