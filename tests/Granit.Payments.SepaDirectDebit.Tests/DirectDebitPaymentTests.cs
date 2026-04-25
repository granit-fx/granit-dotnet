using Granit.Payments.SepaDirectDebit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.SepaDirectDebit.Tests;

public sealed class DirectDebitPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithPositiveAmount_StartsInPendingStatus()
    {
        var p = DirectDebitPayment.Create(
            Guid.NewGuid(), Guid.NewGuid(), 100.50m, "EUR", Now);

        p.Amount.ShouldBe(100.50m);
        p.Currency.ShouldBe("EUR");
        p.ScheduledDate.ShouldBe(Now);
        p.Status.ShouldBe(CollectionStatus.Pending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmount_Throws(decimal amount) =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), amount, "EUR", Now));

    [Fact]
    public void MarkSubmitted_SetsStatusAndSubmittedAt()
    {
        var p = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR", Now);

        p.MarkSubmitted(Now.AddHours(1), "PROV-123");

        p.Status.ShouldBe(CollectionStatus.Submitted);
        p.SubmittedAt.ShouldBe(Now.AddHours(1));
        p.ProviderCollectionId.ShouldBe("PROV-123");
    }

    [Fact]
    public void MarkSucceeded_SetsStatusAndSettledAt()
    {
        var p = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR", Now);

        p.MarkSucceeded(Now.AddDays(2));

        p.Status.ShouldBe(CollectionStatus.Succeeded);
        p.SettledAt.ShouldBe(Now.AddDays(2));
    }

    [Fact]
    public void MarkFailed_StoresFailureCodeAndReason()
    {
        var p = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR", Now);

        p.MarkFailed("AM04", "Insufficient funds");

        p.Status.ShouldBe(CollectionStatus.Failed);
        p.FailureCode.ShouldBe("AM04");
        p.FailureReason.ShouldBe("Insufficient funds");
    }

    [Fact]
    public void MarkRefunded_TransitionsToRefunded()
    {
        var p = DirectDebitPayment.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "EUR", Now);

        p.MarkRefunded();

        p.Status.ShouldBe(CollectionStatus.Refunded);
    }
}
