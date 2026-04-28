using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Response shape for a single party.</summary>
public sealed record PartyResponse(
    Guid Id,
    Guid? TenantId,
    PartyKind Kind,
    string Name,
    string DefaultCurrency,
    string Timezone,
    string? Language,
    string? Website,
    string? TaxId,
    string? RegistrationNumber,
    Guid? ParentPartyId,
    Guid? UserId,
    Guid? AvatarBlobId,
    PartyRoles Roles,
    PartyStatus Status,
    IReadOnlyList<PartyAddressResponse> Addresses,
    IReadOnlyList<PartyEmailResponse> Emails,
    IReadOnlyList<PartyPhoneResponse> Phones,
    IReadOnlyList<PartyExternalMappingResponse> ExternalMappings,
    PartyTaxStatusResponse TaxStatus,
    IReadOnlyDictionary<string, string> Metadata,
    string? InternalNotes);

/// <summary>Customer-specific tax classification (exempt / reverse-charge / standard).</summary>
public sealed record PartyTaxStatusResponse(
    bool IsExempt,
    bool ReverseCharge,
    string? Vatin,
    Guid? EvidenceBlobId);

/// <summary>Address sub-DTO inside <see cref="PartyResponse"/>.</summary>
public sealed record PartyAddressResponse(
    Guid Id,
    AddressKind Kind,
    bool IsDefault,
    string? Label,
    string Line1,
    string City,
    string PostalCode,
    string Country,
    string? CompanyName,
    string? Line2,
    string? State);

/// <summary>Email sub-DTO inside <see cref="PartyResponse"/>.</summary>
public sealed record PartyEmailResponse(
    Guid Id,
    string Address,
    bool IsPrimary,
    string? Label);

/// <summary>Phone sub-DTO inside <see cref="PartyResponse"/>.</summary>
public sealed record PartyPhoneResponse(
    Guid Id,
    PhoneKind Kind,
    string Number,
    bool IsPrimary,
    string? Label);

/// <summary>External-mapping sub-DTO inside <see cref="PartyResponse"/>.</summary>
public sealed record PartyExternalMappingResponse(
    Guid Id,
    string ProviderName,
    string ExternalId);

/// <summary>Lightweight summary used by list endpoints.</summary>
public sealed record PartyListItemResponse(
    Guid Id,
    Guid? TenantId,
    PartyKind Kind,
    string Name,
    PartyRoles Roles,
    PartyStatus Status,
    string DefaultCurrency,
    string? PrimaryEmail,
    string? PrimaryPhone);
