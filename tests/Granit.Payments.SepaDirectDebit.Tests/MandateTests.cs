using Granit.Payments.SepaDirectDebit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Tests;

public sealed class MandateTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private static Mandate NewMandate(string? providerName = null) =>
        Mandate.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "SDD-A1B2", SddScheme.Core,
            "John Doe", "BE68539007547034", "BE12ZZZ0000012345",
            providerName: providerName);

    [Fact]
    public void Create_WithValidArgs_StartsInPendingStatus()
    {
        Mandate m = NewMandate();

        m.Status.ShouldBe(MandateStatus.Pending);
        m.MandateReference.ShouldBe("SDD-A1B2");
        m.DebtorName.ShouldBe("John Doe");
        m.DebtorIban.ShouldBe("BE68539007547034");
        m.CreditorId.ShouldBe("BE12ZZZ0000012345");
        m.SignedAt.ShouldBeNull();
        m.ActivatedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankMandateReference_Throws(string reference) =>
        Should.Throw<ArgumentException>(() =>
            Mandate.Create(Guid.NewGuid(), Guid.NewGuid(),
                reference, SddScheme.Core, "John", "IBAN", "CRED"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankDebtorName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            Mandate.Create(Guid.NewGuid(), Guid.NewGuid(),
                "REF", SddScheme.Core, name, "IBAN", "CRED"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankIban_Throws(string iban) =>
        Should.Throw<ArgumentException>(() =>
            Mandate.Create(Guid.NewGuid(), Guid.NewGuid(),
                "REF", SddScheme.Core, "John", iban, "CRED"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankCreditorId_Throws(string credId) =>
        Should.Throw<ArgumentException>(() =>
            Mandate.Create(Guid.NewGuid(), Guid.NewGuid(),
                "REF", SddScheme.Core, "John", "IBAN", credId));

    [Fact]
    public void Activate_WhenPending_TransitionsToActive_AndStampsTimestamps()
    {
        Mandate m = NewMandate();

        bool result = m.Activate(Now);

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Active);
        m.SignedAt.ShouldBe(Now);
        m.ActivatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Activate_WhenSuspended_ReactivatesAndKeepsOriginalSignedAt()
    {
        Mandate m = NewMandate();
        m.Activate(Now);
        m.Suspend();

        bool result = m.Activate(Now.AddDays(7));

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Active);
        m.SignedAt.ShouldBe(Now); // unchanged
        m.ActivatedAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFalse()
    {
        Mandate m = NewMandate();
        m.Activate(Now);

        bool result = m.Activate(Now.AddDays(1));

        result.ShouldBeFalse();
    }

    [Fact]
    public void Suspend_WhenActive_TransitionsToSuspended()
    {
        Mandate m = NewMandate();
        m.Activate(Now);

        bool result = m.Suspend();

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Suspended);
    }

    [Fact]
    public void Suspend_WhenPending_ReturnsFalse() =>
        NewMandate().Suspend().ShouldBeFalse();

    [Fact]
    public void Cancel_FromActive_TransitionsAndStampsTimestamp()
    {
        Mandate m = NewMandate();
        m.Activate(Now);

        bool result = m.Cancel(Now.AddDays(30));

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Cancelled);
        m.CancelledAt.ShouldBe(Now.AddDays(30));
    }

    [Fact]
    public void Cancel_FromCancelled_ReturnsFalse()
    {
        Mandate m = NewMandate();
        m.Activate(Now);
        m.Cancel(Now);

        bool result = m.Cancel(Now.AddDays(1));

        result.ShouldBeFalse();
    }

    [Fact]
    public void MarkFailed_FromPending_TransitionsToFailed()
    {
        Mandate m = NewMandate();

        bool result = m.MarkFailed();

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Failed);
    }

    [Fact]
    public void MarkFailed_FromActive_ReturnsFalse()
    {
        Mandate m = NewMandate();
        m.Activate(Now);

        m.MarkFailed().ShouldBeFalse();
    }

    [Fact]
    public void Expire_FromActive_TransitionsToExpired()
    {
        Mandate m = NewMandate();
        m.Activate(Now);

        bool result = m.Expire();

        result.ShouldBeTrue();
        m.Status.ShouldBe(MandateStatus.Expired);
    }

    [Fact]
    public void Expire_FromPending_ReturnsFalse() =>
        NewMandate().Expire().ShouldBeFalse();

    [Fact]
    public void SetProviderMandateId_StoresValue()
    {
        Mandate m = NewMandate();
        m.SetProviderMandateId("MD_xxx_provider");

        m.ProviderMandateId.ShouldBe("MD_xxx_provider");
    }

    [Fact]
    public void AddCollection_WhenActive_Appends()
    {
        Mandate m = NewMandate();
        m.Activate(Now);
        var collection = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", Now.AddDays(3));

        m.AddCollection(collection);

        m.Collections.Count.ShouldBe(1);
        m.Collections[0].ShouldBe(collection);
    }

    [Fact]
    public void AddCollection_WhenPending_Throws()
    {
        Mandate m = NewMandate();
        var collection = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, "EUR", Now);

        Should.Throw<InvalidOperationException>(() => m.AddCollection(collection));
    }

    [Fact]
    public void RecordSuccessfulCollection_UpdatesLastCollectionAt()
    {
        Mandate m = NewMandate();

        m.RecordSuccessfulCollection(Now.AddDays(5));

        m.LastCollectionAt.ShouldBe(Now.AddDays(5));
    }
}
