using System.Text.Json;
using Granit.Contacts.Domain;
using Granit.Privacy.DataExport;

namespace Granit.Contacts.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.Contacts. Exports the <see cref="Contact"/> linked to
/// the requesting user (name, emails, phones, addresses, external mappings) as a single
/// JSON fragment.
/// </summary>
/// <remarks>
/// The provider returns an empty buffer when no contact is linked to <c>userId</c> —
/// the privacy saga then records an <c>empty:</c> sentinel for this provider and skips
/// blob upload. Lookups go through <see cref="IContactReader.GetByUserIdAsync"/> which
/// honours the active tenant scope; for cross-tenant exports the caller is expected to
/// have already disabled the multi-tenant filter.
/// </remarks>
public sealed class ContactsPrivacyDataProvider(IContactReader contacts) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "contacts";

    /// <inheritdoc />
    public static string ContentType => "application/json";

    /// <inheritdoc />
    public static string FileName(Guid requestId) => "contacts.json";

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        Contact? contact = await contacts.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        ContactsExportDto dto = new(
            ContactId: contact.Id,
            TenantId: contact.TenantId,
            Kind: contact.Kind.ToString(),
            Name: contact.Name,
            Website: contact.Website,
            Language: contact.Language,
            Timezone: contact.Timezone,
            DefaultCurrency: contact.DefaultCurrency,
            TaxId: contact.TaxId,
            RegistrationNumber: contact.RegistrationNumber,
            Roles: contact.Roles.ToString(),
            Status: contact.Status.ToString(),
            Emails: [.. contact.Emails.Select(e => new ContactEmailDto(e.Address, e.IsPrimary, e.Label))],
            Phones: [.. contact.Phones.Select(p => new ContactPhoneDto(p.Kind.ToString(), p.Number, p.IsPrimary, p.Label))],
            Addresses: [.. contact.Addresses.Select(a => new ContactAddressDto(
                a.Kind.ToString(),
                a.IsDefault,
                a.Label,
                a.Value.Line1,
                a.Value.Line2,
                a.Value.City,
                a.Value.State,
                a.Value.PostalCode,
                a.Value.Country,
                a.Value.CompanyName))],
            ExternalMappings: [.. contact.ExternalMappings.Select(m => new ContactExternalMappingDto(m.ProviderName, m.ExternalId))]);

        return JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record ContactsExportDto(
    Guid ContactId,
    Guid? TenantId,
    string Kind,
    string Name,
    string? Website,
    string? Language,
    string Timezone,
    string DefaultCurrency,
    string? TaxId,
    string? RegistrationNumber,
    string Roles,
    string Status,
    IReadOnlyList<ContactEmailDto> Emails,
    IReadOnlyList<ContactPhoneDto> Phones,
    IReadOnlyList<ContactAddressDto> Addresses,
    IReadOnlyList<ContactExternalMappingDto> ExternalMappings);

internal sealed record ContactEmailDto(string Address, bool IsPrimary, string? Label);

internal sealed record ContactPhoneDto(string Kind, string Number, bool IsPrimary, string? Label);

internal sealed record ContactAddressDto(
    string Kind,
    bool IsDefault,
    string? Label,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country,
    string? CompanyName);

internal sealed record ContactExternalMappingDto(string ProviderName, string ExternalId);
