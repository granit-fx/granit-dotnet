using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests;

public sealed class PaymentTransactionTests
{
    private static PaymentTransaction CreateTransaction() =>
        PaymentTransaction.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount: 100m,
            "EUR",
            "stripe",
            "card",
            $"idem_{Guid.NewGuid()}");

    [Fact]
    public void Create_ShouldSetCreatedStatus()
    {
        PaymentTransaction tx = CreateTransaction();

        tx.Status.ShouldBe(PaymentStatus.Created);
        tx.Amount.ShouldBe(100m);
        tx.Currency.ShouldBe("EUR");
        tx.ProviderName.ShouldBe("stripe");
    }

    [Fact]
    public void MarkRequiresAction_ShouldTransition()
    {
        PaymentTransaction tx = CreateTransaction();

        bool result = tx.MarkRequiresAction("https://stripe.com/3ds");

        result.ShouldBeTrue();
        tx.Status.ShouldBe(PaymentStatus.RequiresAction);
        tx.ActionUrl.ShouldBe("https://stripe.com/3ds");
    }

    [Fact]
    public void MarkProcessing_FromCreated_ShouldTransition()
    {
        PaymentTransaction tx = CreateTransaction();

        bool result = tx.MarkProcessing();

        result.ShouldBeTrue();
        tx.Status.ShouldBe(PaymentStatus.Processing);
    }

    [Fact]
    public void MarkSucceeded_ShouldTransition()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkProcessing();
        DateTimeOffset succeededAt = DateTimeOffset.UtcNow;

        bool result = tx.MarkSucceeded("pi_123", succeededAt);

        result.ShouldBeTrue();
        tx.Status.ShouldBe(PaymentStatus.Succeeded);
        tx.ProviderTransactionId.ShouldBe("pi_123");
        tx.SucceededAt.ShouldBe(succeededAt);
    }

    [Fact]
    public void MarkFailed_ShouldTransition()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkProcessing();

        bool result = tx.MarkFailed("card_declined", "Your card was declined.");

        result.ShouldBeTrue();
        tx.Status.ShouldBe(PaymentStatus.Failed);
        tx.FailureCode.ShouldBe("card_declined");
    }

    [Fact]
    public void Cancel_ShouldTransition()
    {
        PaymentTransaction tx = CreateTransaction();
        DateTimeOffset canceledAt = DateTimeOffset.UtcNow;

        bool result = tx.Cancel(canceledAt);

        result.ShouldBeTrue();
        tx.Status.ShouldBe(PaymentStatus.Canceled);
        tx.CanceledAt.ShouldBe(canceledAt);
    }

    [Fact]
    public void RequestRefund_OnSucceeded_ShouldAddRefund()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkProcessing();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        Refund refund = tx.RequestRefund(Guid.NewGuid(), 30m, DateTimeOffset.UtcNow, "Partial refund");

        tx.Refunds.Count.ShouldBe(1);
        refund.Amount.ShouldBe(30m);
        refund.Reason.ShouldBe("Partial refund");
    }

    [Fact]
    public void RequestRefund_ExceedingAmount_ShouldThrow()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkProcessing();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        tx.RequestRefund(Guid.NewGuid(), 80m, DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            tx.RequestRefund(Guid.NewGuid(), 30m, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void OpenDispute_ShouldAddDispute()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkProcessing();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        Dispute dispute = tx.OpenDispute(
            Guid.NewGuid(), "dp_123", "Fraudulent", 100m, DateTimeOffset.UtcNow);

        tx.Disputes.Count.ShouldBe(1);
        dispute.Status.ShouldBe(DisputeStatus.Open);
        dispute.Reason.ShouldBe("Fraudulent");
    }

    // ======== Invalid transitions ========

    [Fact]
    public void MarkSucceeded_FromFailed_ShouldThrow()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkFailed("card_declined", "Declined");

        Should.Throw<InvalidOperationException>(() =>
            tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void MarkFailed_FromSucceeded_ShouldThrow()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            tx.MarkFailed("error", "Something went wrong"));
    }

    [Fact]
    public void Cancel_FromSucceeded_ShouldThrow()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        Should.Throw<InvalidOperationException>(() =>
            tx.Cancel(DateTimeOffset.UtcNow));
    }

    // ======== Idempotent transitions ========

    [Fact]
    public void MarkSucceeded_Twice_ShouldReturnFalse()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        bool result = tx.MarkSucceeded("pi_456", DateTimeOffset.UtcNow);

        result.ShouldBeFalse();
    }

    // ======== CompleteRefund flow ========

    [Fact]
    public void CompleteRefund_ShouldMarkRefundSucceeded()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);
        var refundId = Guid.NewGuid();
        tx.RequestRefund(refundId, 30m, DateTimeOffset.UtcNow);

        tx.CompleteRefund(refundId, "re_123", DateTimeOffset.UtcNow);

        tx.Refunds[0].Status.ShouldBe(RefundStatus.Succeeded);
        tx.Refunds[0].ProviderRefundId.ShouldBe("re_123");
    }

    // ======== Dispute resolution ========

    [Fact]
    public void Dispute_Resolve_Won_ShouldUpdateStatus()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);
        Dispute dispute = tx.OpenDispute(Guid.NewGuid(), "dp_123", "Fraudulent", 100m, DateTimeOffset.UtcNow);
        DateTimeOffset resolvedAt = DateTimeOffset.UtcNow;

        dispute.Resolve(DisputeStatus.Won, resolvedAt);

        dispute.Status.ShouldBe(DisputeStatus.Won);
        dispute.ResolvedAt.ShouldBe(resolvedAt);
    }

    [Fact]
    public void Dispute_Resolve_Lost_ShouldUpdateStatus()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);
        Dispute dispute = tx.OpenDispute(Guid.NewGuid(), "dp_123", "Fraudulent", 100m, DateTimeOffset.UtcNow);

        dispute.Resolve(DisputeStatus.Lost, DateTimeOffset.UtcNow);

        dispute.Status.ShouldBe(DisputeStatus.Lost);
    }

    // ======== Multiple refunds ========

    [Fact]
    public void RequestRefund_MultipleRefunds_ExactAmount_ShouldSucceed()
    {
        PaymentTransaction tx = CreateTransaction();
        tx.MarkSucceeded("pi_123", DateTimeOffset.UtcNow);

        tx.RequestRefund(Guid.NewGuid(), 40m, DateTimeOffset.UtcNow);
        tx.RequestRefund(Guid.NewGuid(), 60m, DateTimeOffset.UtcNow);

        tx.Refunds.Count.ShouldBe(2);
    }
}
