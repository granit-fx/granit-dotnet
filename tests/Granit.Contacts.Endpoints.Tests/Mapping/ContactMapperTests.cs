using Granit.Contacts.Domain;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Contacts.Endpoints.Mapping;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Endpoints.Tests.Mapping;

public sealed class ContactMapperTests
{
    [Fact]
    public void ToResponse_PopulatesAllFields()
    {
        var c = Contact.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            ContactKind.Company, "Acme", "EUR",
            roles: ContactRoles.Customer | ContactRoles.Supplier,
            taxId: "BE0123456789",
            registrationNumber: "0123.456.789");
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "billing@acme.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+3221234567");
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        ContactResponse response = c.ToResponse();

        response.Id.ShouldBe(c.Id);
        response.Kind.ShouldBe(ContactKind.Company);
        response.Name.ShouldBe("Acme");
        response.DefaultCurrency.ShouldBe("EUR");
        response.Roles.ShouldBe(ContactRoles.Customer | ContactRoles.Supplier);
        response.Status.ShouldBe(ContactStatus.Active);
        response.TaxId.ShouldBe("BE0123456789");
        response.RegistrationNumber.ShouldBe("0123.456.789");
        response.Addresses.ShouldHaveSingleItem();
        response.Emails.ShouldHaveSingleItem();
        response.Phones.ShouldHaveSingleItem();
        response.ExternalMappings.ShouldHaveSingleItem();
    }

    [Fact]
    public void ToListItem_IncludesPrimaryEmailAndPhone()
    {
        var c = Contact.Create(
            Guid.NewGuid(), null, ContactKind.Individual, "Jean", "EUR");
        c.AddEmail(Guid.NewGuid(), "jean@example.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+33611223344");

        ContactListItemResponse item = c.ToListItem();

        item.Id.ShouldBe(c.Id);
        item.Kind.ShouldBe(ContactKind.Individual);
        item.Name.ShouldBe("Jean");
        item.PrimaryEmail.ShouldBe("jean@example.com");
        item.PrimaryPhone.ShouldBe("+33611223344");
    }

    [Fact]
    public void ToListItem_NoEmail_NoPhone_ReturnsNull()
    {
        var c = Contact.Create(
            Guid.NewGuid(), null, ContactKind.Company, "X", "EUR");

        ContactListItemResponse item = c.ToListItem();

        item.PrimaryEmail.ShouldBeNull();
        item.PrimaryPhone.ShouldBeNull();
    }
}
