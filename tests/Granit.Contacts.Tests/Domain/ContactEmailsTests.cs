using Granit.Contacts.Domain;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Tests.Domain;

public sealed class ContactEmailsTests
{
    private static Contact New() =>
        Contact.Create(Guid.NewGuid(), null, ContactKind.Company, "Acme", "EUR");

    [Fact]
    public void AddEmail_FirstOne_AutoMarksPrimary()
    {
        Contact c = New();
        var id = Guid.NewGuid();

        c.AddEmail(id, "billing@acme.com");

        c.Emails.ShouldHaveSingleItem();
        c.Emails[0].IsPrimary.ShouldBeTrue();
        c.PrimaryEmail.ShouldBe(c.Emails[0]);
    }

    [Fact]
    public void AddEmail_Second_NotPrimaryByDefault()
    {
        Contact c = New();
        c.AddEmail(Guid.NewGuid(), "first@acme.com");
        var secondId = Guid.NewGuid();
        c.AddEmail(secondId, "second@acme.com");

        c.Emails.Single(e => e.Id == secondId).IsPrimary.ShouldBeFalse();
    }

    [Fact]
    public void AddEmail_WithIsPrimaryTrue_DemotesPreviousPrimary()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        c.AddEmail(firstId, "first@acme.com");
        var secondId = Guid.NewGuid();
        c.AddEmail(secondId, "second@acme.com", isPrimary: true);

        c.Emails.Single(e => e.Id == firstId).IsPrimary.ShouldBeFalse();
        c.Emails.Single(e => e.Id == secondId).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void AddEmail_StoresLabel()
    {
        Contact c = New();
        c.AddEmail(Guid.NewGuid(), "billing@acme.com", label: "billing");

        c.Emails[0].Label.ShouldBe("billing");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddEmail_BlankAddress_Throws(string address) =>
        Should.Throw<ArgumentException>(() => New().AddEmail(Guid.NewGuid(), address));

    [Fact]
    public void RemoveEmail_KnownId_PromotesAnother()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddEmail(firstId, "a@x.com");
        c.AddEmail(secondId, "b@x.com");

        c.RemoveEmail(firstId).ShouldBeTrue();

        c.Emails.ShouldHaveSingleItem();
        c.Emails[0].Id.ShouldBe(secondId);
        c.Emails[0].IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void RemoveEmail_OnlyOne_LeavesNoPrimary()
    {
        Contact c = New();
        var id = Guid.NewGuid();
        c.AddEmail(id, "a@x.com");

        c.RemoveEmail(id).ShouldBeTrue();

        c.Emails.ShouldBeEmpty();
        c.PrimaryEmail.ShouldBeNull();
    }

    [Fact]
    public void RemoveEmail_UnknownId_ReturnsFalse() =>
        New().RemoveEmail(Guid.NewGuid()).ShouldBeFalse();

    [Fact]
    public void UpdateEmail_KnownId_ReplacesAddress()
    {
        Contact c = New();
        var id = Guid.NewGuid();
        c.AddEmail(id, "old@x.com");

        c.UpdateEmail(id, "new@x.com", label: "updated");

        c.Emails[0].Address.ShouldBe("new@x.com");
        c.Emails[0].Label.ShouldBe("updated");
    }

    [Fact]
    public void UpdateEmail_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            New().UpdateEmail(Guid.NewGuid(), "x@x.com"));

    [Fact]
    public void SetPrimaryEmail_DemotesPreviousAndPromotesTarget()
    {
        Contact c = New();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        c.AddEmail(firstId, "a@x.com");
        c.AddEmail(secondId, "b@x.com");

        c.SetPrimaryEmail(secondId);

        c.Emails.Single(e => e.Id == firstId).IsPrimary.ShouldBeFalse();
        c.Emails.Single(e => e.Id == secondId).IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void SetPrimaryEmail_AlreadyPrimary_NoOp()
    {
        Contact c = New();
        var id = Guid.NewGuid();
        c.AddEmail(id, "a@x.com");

        Should.NotThrow(() => c.SetPrimaryEmail(id));
        c.Emails[0].IsPrimary.ShouldBeTrue();
    }

    [Fact]
    public void SetPrimaryEmail_UnknownId_Throws() =>
        Should.Throw<InvalidOperationException>(() => New().SetPrimaryEmail(Guid.NewGuid()));

    [Fact]
    public void AddEmail_OnArchived_Throws()
    {
        Contact c = New();
        c.Archive();
        Should.Throw<InvalidOperationException>(() => c.AddEmail(Guid.NewGuid(), "x@x.com"));
    }
}
