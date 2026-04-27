using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Mapping;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Mapping;

public sealed class PartyMapperTests
{
    [Fact]
    public void ToResponse_PopulatesAllFields()
    {
        var c = Party.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PartyKind.Company, "Acme", "EUR",
            roles: PartyRoles.Customer | PartyRoles.Supplier,
            taxId: "BE0123456789",
            registrationNumber: "0123.456.789");
        c.AddAddress(Guid.NewGuid(), AddressKind.Billing, Address.Create("L1", "C", "1000", "BE"));
        c.AddEmail(Guid.NewGuid(), "billing@acme.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+3221234567");
        c.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_1");

        PartyResponse response = c.ToResponse();

        response.Id.ShouldBe(c.Id);
        response.Kind.ShouldBe(PartyKind.Company);
        response.Name.ShouldBe("Acme");
        response.DefaultCurrency.ShouldBe("EUR");
        response.Roles.ShouldBe(PartyRoles.Customer | PartyRoles.Supplier);
        response.Status.ShouldBe(PartyStatus.Active);
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
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Individual, "Jean", "EUR");
        c.AddEmail(Guid.NewGuid(), "jean@example.com");
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+33611223344");

        PartyListItemResponse item = c.ToListItem();

        item.Id.ShouldBe(c.Id);
        item.Kind.ShouldBe(PartyKind.Individual);
        item.Name.ShouldBe("Jean");
        item.PrimaryEmail.ShouldBe("jean@example.com");
        item.PrimaryPhone.ShouldBe("+33611223344");
    }

    [Fact]
    public void ToListItem_NoEmail_NoPhone_ReturnsNull()
    {
        var c = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "X", "EUR");

        PartyListItemResponse item = c.ToListItem();

        item.PrimaryEmail.ShouldBeNull();
        item.PrimaryPhone.ShouldBeNull();
    }
}
