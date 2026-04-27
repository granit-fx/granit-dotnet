using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Internal;

public sealed class VCardBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Build_Individual_EmitsRequiredVCard4Properties()
    {
        var p = Party.Create(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            tenantId: null, PartyKind.Individual, "Alice Martin", "EUR",
            website: "https://alice.example",
            language: "fr-BE", timezone: "Europe/Brussels");
        p.AddEmail(Guid.NewGuid(), "alice@example.com", isPrimary: true);
        p.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+32475123456", isPrimary: true);
        p.AddAddress(Guid.NewGuid(), AddressKind.Billing,
            Address.Create("Rue 1", "Brussels", "1000", "BE"));

        string vcard = VCardBuilder.Build(p, Now);

        vcard.ShouldStartWith("BEGIN:VCARD\r\n");
        vcard.ShouldContain("VERSION:4.0\r\n");
        vcard.ShouldContain("UID:urn:uuid:00000000-0000-0000-0000-000000000001\r\n");
        vcard.ShouldContain("FN:Alice Martin\r\n");
        vcard.ShouldContain("N:Martin;Alice;;;\r\n");      // structured name (family;given)
        vcard.ShouldContain("URL:https://alice.example\r\n");
        vcard.ShouldContain("LANG:fr-BE\r\n");
        vcard.ShouldContain("TZ:Europe/Brussels\r\n");
        vcard.ShouldContain("EMAIL;TYPE=INTERNET,pref:alice@example.com\r\n");
        vcard.ShouldContain("TEL;TYPE=cell,pref;VALUE=uri:tel:+32475123456\r\n");
        vcard.ShouldContain("ADR;TYPE=work,pref:;;Rue 1;Brussels;;1000;BE\r\n");
        vcard.ShouldContain("REV:20260427T090000Z\r\n");
        vcard.ShouldEndWith("END:VCARD\r\n");
    }

    [Fact]
    public void Build_Company_EmitsOrgPropertyAndStructuredN()
    {
        var p = Party.Create(
            Guid.NewGuid(), null, PartyKind.Company, "Acme Corporation", "EUR");

        string vcard = VCardBuilder.Build(p, Now);

        vcard.ShouldContain("FN:Acme Corporation\r\n");
        vcard.ShouldContain("N:Acme Corporation;;;;\r\n");
        vcard.ShouldContain("ORG:Acme Corporation\r\n");
    }

    [Fact]
    public void Build_PhoneKindWork_MapsToVCardWorkType()
    {
        var p = Party.Create(Guid.NewGuid(), null, PartyKind.Individual, "Bob", "EUR");
        // First phone is auto-promoted to primary, so it carries the "pref" parameter.
        p.AddPhone(Guid.NewGuid(), PhoneKind.Work, "+3221234567");

        string vcard = VCardBuilder.Build(p, Now);

        vcard.ShouldContain("TEL;TYPE=work,pref;VALUE=uri:tel:+3221234567\r\n");
    }

    [Fact]
    public void Build_EscapesSemicolonsAndCommasInValues()
    {
        var p = Party.Create(Guid.NewGuid(), null, PartyKind.Company, "Acme; Inc., Ltd.", "EUR");

        string vcard = VCardBuilder.Build(p, Now);

        vcard.ShouldContain(@"FN:Acme\; Inc.\, Ltd.");
    }

    [Fact]
    public void SuggestedFileName_SanitizesNonAlphanumericChars()
    {
        var p = Party.Create(Guid.NewGuid(), null, PartyKind.Company, "Acme/Inc & Co.", "EUR");

        string filename = VCardBuilder.SuggestedFileName(p);

        filename.ShouldEndWith(".vcf");
        filename.ShouldNotContain("/");
        filename.ShouldNotContain("&");
        filename.ShouldNotContain(" ");
    }

    [Fact]
    public void SuggestedFileName_EmptyAfterSanitization_FallsBackToPartyId()
    {
        var p = Party.Create(Guid.NewGuid(), null, PartyKind.Company, "###", "EUR");

        string filename = VCardBuilder.SuggestedFileName(p);

        filename.ShouldStartWith("party-");
        filename.ShouldEndWith(".vcf");
    }
}
