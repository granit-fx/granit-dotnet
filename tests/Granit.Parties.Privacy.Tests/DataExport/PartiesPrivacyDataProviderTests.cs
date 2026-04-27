using System.Text.Json;
using Granit.Parties;
using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;
using Granit.Parties.Privacy.DataExport;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Parties.Privacy.Tests.DataExport;

public sealed class ContactsPrivacyDataProviderTests
{
    private readonly IPartyReader _reader = Substitute.For<IPartyReader>();

    private PartiesPrivacyDataProvider Sut() => new(_reader);

    [Fact]
    public void ProviderName_IsContacts() =>
        PartiesPrivacyDataProvider.ProviderName.ShouldBe("parties");

    [Fact]
    public void ContentType_IsApplicationJson() =>
        PartiesPrivacyDataProvider.ContentType.ShouldBe("application/json");

    [Fact]
    public void FileName_IsStable() =>
        PartiesPrivacyDataProvider.FileName(Guid.NewGuid()).ShouldBe("contacts.json");

    [Fact]
    public async Task ExportAsync_NoLinkedContact_ReturnsEmpty()
    {
        _reader.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Party?)null);

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithLinkedContact_ReturnsJsonPayload()
    {
        var userId = Guid.NewGuid();
        var contact = Party.Create(
            Guid.NewGuid(), null, PartyKind.Individual, "Jean Dupont", "EUR",
            taxId: "BE0123456789", registrationNumber: "0123.456.789");
        contact.AddEmail(Guid.NewGuid(), "jean@example.com");
        contact.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+33611223344");
        contact.AddAddress(Guid.NewGuid(), AddressKind.Billing,
            Address.Create("rue 1", "Brussels", "1000", "BE"));
        contact.AddExternalMapping(Guid.NewGuid(), "stripe", "cus_42");
        contact.LinkToUser(userId);

        _reader.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(contact);

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(userId, TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeFalse();
        using var doc = JsonDocument.Parse(result.ToArray());
        JsonElement root = doc.RootElement;
        root.GetProperty("name").GetString().ShouldBe("Jean Dupont");
        root.GetProperty("kind").GetString().ShouldBe("Individual");
        root.GetProperty("emails").GetArrayLength().ShouldBe(1);
        root.GetProperty("phones").GetArrayLength().ShouldBe(1);
        root.GetProperty("addresses").GetArrayLength().ShouldBe(1);
        root.GetProperty("externalMappings").GetArrayLength().ShouldBe(1);
        root.GetProperty("taxId").GetString().ShouldBe("BE0123456789");
    }
}
