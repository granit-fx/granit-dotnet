using Granit.Contacts.Domain;

namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Response shape for a single contact.</summary>
public sealed record ContactResponse(
    Guid Id,
    Guid? TenantId,
    ContactKind Kind,
    string Name,
    string DefaultCurrency,
    string Timezone,
    string? Language,
    string? Website,
    string? TaxId,
    string? RegistrationNumber,
    Guid? ParentContactId,
    Guid? UserId,
    Guid? AvatarBlobId,
    ContactRoles Roles,
    ContactStatus Status,
    IReadOnlyList<ContactAddressResponse> Addresses,
    IReadOnlyList<ContactEmailResponse> Emails,
    IReadOnlyList<ContactPhoneResponse> Phones,
    IReadOnlyList<ContactExternalMappingResponse> ExternalMappings,
    ContactTaxStatusResponse TaxStatus);

/// <summary>Customer-specific tax classification (exempt / reverse-charge / standard).</summary>
public sealed record ContactTaxStatusResponse(
    bool IsExempt,
    bool ReverseCharge,
    string? Vatin,
    Guid? EvidenceBlobId);

/// <summary>Address sub-DTO inside <see cref="ContactResponse"/>.</summary>
public sealed record ContactAddressResponse(
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

/// <summary>Email sub-DTO inside <see cref="ContactResponse"/>.</summary>
public sealed record ContactEmailResponse(
    Guid Id,
    string Address,
    bool IsPrimary,
    string? Label);

/// <summary>Phone sub-DTO inside <see cref="ContactResponse"/>.</summary>
public sealed record ContactPhoneResponse(
    Guid Id,
    PhoneKind Kind,
    string Number,
    bool IsPrimary,
    string? Label);

/// <summary>External-mapping sub-DTO inside <see cref="ContactResponse"/>.</summary>
public sealed record ContactExternalMappingResponse(
    Guid Id,
    string ProviderName,
    string ExternalId);

/// <summary>Lightweight summary used by list endpoints.</summary>
public sealed record ContactListItemResponse(
    Guid Id,
    Guid? TenantId,
    ContactKind Kind,
    string Name,
    ContactRoles Roles,
    ContactStatus Status,
    string DefaultCurrency,
    string? PrimaryEmail,
    string? PrimaryPhone);
