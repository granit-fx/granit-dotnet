using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Parties.Domain;
using Granit.Privacy.DataExport;

namespace Granit.Parties.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.Parties. Exports the <see cref="Party"/> linked to
/// the requesting user (name, emails, phones, addresses, external mappings) as a single
/// JSON fragment.
/// </summary>
/// <remarks>
/// GDPR Article 15 requires the export to be complete regardless of the active tenant
/// scope: a single user may be linked to a host-scoped or tenant-scoped party and the
/// privacy stack must locate either without leaking tenant context. The lookup therefore
/// disables the <see cref="IMultiTenant"/> filter for the duration of the read, mirroring
/// the deletion handler. Returns an empty buffer when no party is linked to
/// <c>userId</c> — the privacy saga then records an <c>empty:</c> sentinel for this
/// provider and skips blob upload.
/// </remarks>
public sealed class PartiesPrivacyDataProvider(
    IPartyReader parties,
    IDataFilter dataFilter) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "parties";

    /// <inheritdoc />
    public static string ContentType => "application/json";

    /// <inheritdoc />
    public static string FileName(Guid requestId) => "parties.json";

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        Party? party;
        using (dataFilter.Disable<IMultiTenant>())
        {
            party = await parties.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        if (party is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        PartiesExportDto dto = new(
            PartyId: party.Id,
            TenantId: party.TenantId,
            Kind: party.Kind.ToString(),
            Name: party.Name,
            Website: party.Website,
            Language: party.Language,
            Timezone: party.Timezone,
            DefaultCurrency: party.DefaultCurrency,
            TaxId: party.TaxId,
            RegistrationNumber: party.RegistrationNumber,
            Roles: party.Roles.ToString(),
            Status: party.Status.ToString(),
            Emails: [.. party.Emails.Select(e => new PartyEmailDto(e.Address, e.IsPrimary, e.Label))],
            Phones: [.. party.Phones.Select(p => new PartyPhoneDto(p.Kind.ToString(), p.Number, p.IsPrimary, p.Label))],
            Addresses: [.. party.Addresses.Select(a => new PartyAddressDto(
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
            ExternalMappings: [.. party.ExternalMappings.Select(m => new PartyExternalMappingDto(m.ProviderName, m.ExternalId))]);

        return JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record PartiesExportDto(
    Guid PartyId,
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
    IReadOnlyList<PartyEmailDto> Emails,
    IReadOnlyList<PartyPhoneDto> Phones,
    IReadOnlyList<PartyAddressDto> Addresses,
    IReadOnlyList<PartyExternalMappingDto> ExternalMappings);

internal sealed record PartyEmailDto(string Address, bool IsPrimary, string? Label);

internal sealed record PartyPhoneDto(string Kind, string Number, bool IsPrimary, string? Label);

internal sealed record PartyAddressDto(
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

internal sealed record PartyExternalMappingDto(string ProviderName, string ExternalId);
