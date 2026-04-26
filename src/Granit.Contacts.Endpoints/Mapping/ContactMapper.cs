using Granit.Contacts.Domain;
using Granit.Contacts.Endpoints.Dtos;

namespace Granit.Contacts.Endpoints.Mapping;

internal static class ContactMapper
{
    public static ContactResponse ToResponse(this Contact c) => new(
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
        [.. c.ExternalMappings.Select(ToExternalMappingResponse)]);

    public static ContactListItemResponse ToListItem(this Contact c) => new(
        c.Id,
        c.TenantId,
        c.Kind,
        c.Name,
        c.Roles,
        c.Status,
        c.DefaultCurrency,
        c.PrimaryEmail?.Address,
        c.PrimaryPhone?.Number);

    private static ContactAddressResponse ToAddressResponse(ContactAddress a) => new(
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

    private static ContactEmailResponse ToEmailResponse(ContactEmail e) =>
        new(e.Id, e.Address, e.IsPrimary, e.Label);

    private static ContactPhoneResponse ToPhoneResponse(ContactPhone p) =>
        new(p.Id, p.Kind, p.Number, p.IsPrimary, p.Label);

    private static ContactExternalMappingResponse ToExternalMappingResponse(ContactExternalMapping m) =>
        new(m.Id, m.ProviderName, m.ExternalId);
}
