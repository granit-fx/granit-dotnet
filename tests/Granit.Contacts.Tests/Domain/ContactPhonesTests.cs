using Granit.Contacts.Domain;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Tests.Domain;

public sealed class ContactPhonesTests
{
    private static Contact New() =>
        Contact.Create(Guid.NewGuid(), null, ContactKind.Individual, "Jean", "EUR");

    [Fact]
    public void AddPhone_FirstOne_AutoMarksPrimary()
    {
        Contact c = New();
        var id = Guid.NewGuid();

        c.AddPhone(id, PhoneKind.Mobile, "+32475123456");

        c.Phones.ShouldHaveSingleItem();
        c.Phones[0].IsPrimary.ShouldBeTrue();
        c.Phones[0].Kind.ShouldBe(PhoneKind.Mobile);
        c.PrimaryPhone.ShouldBe(c.Phones[0]);
    }

    [Theory]
    [InlineData(PhoneKind.Mobile)]
    [InlineData(PhoneKind.Office)]
    [InlineData(PhoneKind.Home)]
    [InlineData(PhoneKind.Other)]
    public void AddPhone_AnyKind_StoresIt(PhoneKind kind)
    {
        Contact c = New();
        c.AddPhone(Guid.NewGuid(), kind, "+12345678901");
        c.Phones[0].Kind.ShouldBe(kind);
    }

    [Fact]
    public void AddPhone_Second_NotPrimaryByDefault()
    {
        Contact c = New();
        c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+1");
        var id = Guid.NewGuid();
        c.AddPhone(id, PhoneKind.Office, "+2");

        c.Phones.Single(p => p.Id == id).IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void AddPhone_WithIsPrimaryTrue_DemotesPreviousPrimary()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        c.AddPhone(firstId, PhoneKind.Office, "+1");
        var secondId = Guid.NewGuid();
        c.AddPhone(secondId, PhoneKind.Mobile, "+2", isPrimary: true);

        c.Phones.Single(p => p.Id == firstId).IsPrimary.ShouldBeFalse();
        c.Phones.Single(p => p.Id == secondId).IsPrimary.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddPhone_BlankNumber_Throws(string number) =>
        Should.Throw<ArgumentException>(() =>
            New().AddPhone(Guid.NewGuid(), PhoneKind.Mobile, number));

    [Fact]
    public void RemovePhone_KnownId_PromotesAnother()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddPhone(firstId, PhoneKind.Office, "+1");
        c.AddPhone(secondId, PhoneKind.Mobile, "+2");

        c.RemovePhone(firstId).ShouldBeTrue();

        c.Phones.ShouldHaveSingleItem();
        c.Phones[0].Id.ShouldBe(secondId);
        c.Phones[0].IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void RemovePhone_OnlyOne_LeavesNoPrimary()
    {
        Contact c = New();
        var id = Guid.NewGuid();
        c.AddPhone(id, PhoneKind.Mobile, "+1");

        c.RemovePhone(id).ShouldBeTrue();
        c.Phones.ShouldBeEmpty();
        c.PrimaryPhone.ShouldBeNull();
    }

    [Fact]
    public void RemovePhone_UnknownId_ReturnsFalse() =>
        New().RemovePhone(Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public void UpdatePhone_KnownId_ReplacesKindNumberLabel()
    {
        Contact c = New();
        var id = Guid.NewGuid();
        c.AddPhone(id, PhoneKind.Office, "+1", label: "old");

        c.UpdatePhone(id, PhoneKind.Mobile, "+2", label: "new");

        c.Phones[0].Kind.ShouldBe(PhoneKind.Mobile);
        c.Phones[0].Number.ShouldBe("+2");
        c.Phones[0].Label.ShouldBe("new");
    }

    [Fact]
    public void UpdatePhone_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            New().UpdatePhone(Guid.NewGuid(), PhoneKind.Mobile, "+1"));

    [Fact]
    public void SetPrimaryPhone_DemotesPreviousAndPromotesTarget()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddPhone(firstId, PhoneKind.Office, "+1");
        c.AddPhone(secondId, PhoneKind.Mobile, "+2");

        c.SetPrimaryPhone(secondId);

        c.Phones.Single(p => p.Id == firstId).IsPrimary.ShouldBeFalse();
        c.Phones.Single(p => p.Id == secondId).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void SetPrimaryPhone_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() => New().SetPrimaryPhone(Guid.NewGuid()));

    [Fact]
    public void AddPhone_OnArchived_Throws()
    {
        Contact c = New();
        c.Archive();
        Should.Throw<InvalidOperationException>(() =>
            c.AddPhone(Guid.NewGuid(), PhoneKind.Mobile, "+1"));
    }
}
