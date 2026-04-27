using Granit.Domain;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;

namespace Granit.Parties.Endpoints.Mapping;

internal static class PartyMapper
{
    public static PartyResponse ToResponse(this Party c) => new(
        c.Id,
        c.TenantId,
        c.Kind,
        c.Name,
        c.DefaultCurrency,
        c.Timezone,
        c.Language,
        c.Website,
        c.TaxId,
        c.RegistrationNumber,
        c.ParentContactId is { } pid ? pid.Value : null,
        c.UserId,
        c.AvatarBlobId,
        c.Roles,
        c.Status,
        [.. c.Addresses.Select(ToAddressResponse)],
        [.. c.Emails.Select(ToEmailResponse)],
        [.. c.Phones.Select(ToPhoneResponse)],
        [.. c.ExternalMappings.Select(ToExternalMappingResponse)],
        ToTaxStatusResponse(c.TaxStatus),
        c.GetMetadata(),
        c.InternalNotes);

    private static PartyTaxStatusResponse ToTaxStatusResponse(TaxStatus s) =>
        new(s.IsExempt, s.ReverseCharge, s.Vatin, s.EvidenceBlobId);

    public static PartyListItemResponse ToListItem(this Party c) => new(
        c.Id,
        c.TenantId,
        c.Kind,
        c.Name,
        c.Roles,
        c.Status,
        c.DefaultCurrency,
        c.PrimaryEmail?.Address,
        c.PrimaryPhone?.Number);

    private static PartyAddressResponse ToAddressResponse(PartyAddress a) => new(
        a.Id,
        a.Kind,
        a.IsDefault,
        a.Label,
        a.Value.Line1,
        a.Value.City,
        a.Value.PostalCode,
        a.Value.Country,
        a.Value.CompanyName,
        a.Value.Line2,
        a.Value.State);

    private static PartyEmailResponse ToEmailResponse(PartyEmail e) =>
        new(e.Id, e.Address, e.IsPrimary, e.Label);

    private static PartyPhoneResponse ToPhoneResponse(PartyPhone p) =>
        new(p.Id, p.Kind, p.Number, p.IsPrimary, p.Label);

    private static PartyExternalMappingResponse ToExternalMappingResponse(PartyExternalMapping m) =>
        new(m.Id, m.ProviderName, m.ExternalId);
}
