using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Domain;

public sealed class RefundTests
{
    // ======== Create ========

    [Fact]
    public void Create_ShouldSetPendingStatus()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;

        var refund = Refund.Create(Guid.NewGuid(), 50m, "EUR", createdAt, "Customer request");

        refund.Status.ShouldBe(RefundStatus.Pending);
        refund.Amount.ShouldBe(50m);
        refund.Currency.ShouldBe("EUR");
        refund.Reason.ShouldBe("Customer request");
        refund.CreatedAt.ShouldBe(createdAt);
        refund.CompletedAt.ShouldBeNull();
        refund.ProviderRefundId.ShouldBeNull();
    }

    [Fact]
    public void Create_WithoutReason_ShouldSetReasonNull()
    {
        var refund = Refund.Create(Guid.NewGuid(), 25m, "USD", DateTimeOffset.UtcNow);

        refund.Reason.ShouldBeNull();
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Refund.Create(Guid.NewGuid(), 0m, "EUR", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Refund.Create(Guid.NewGuid(), -10m, "EUR", DateTimeOffset.UtcNow));
    }

    // ======== MarkSucceeded ========

    [Fact]
    public void MarkSucceeded_ShouldTransitionToSucceeded()
    {
        var refund = Refund.Create(Guid.NewGuid(), 50m, "EUR", DateTimeOffset.UtcNow);
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;

        refund.MarkSucceeded("re_stripe_123", completedAt);

        refund.Status.ShouldBe(RefundStatus.Succeeded);
        refund.ProviderRefundId.ShouldBe("re_stripe_123");
        refund.CompletedAt.ShouldBe(completedAt);
    }

    // ======== MarkFailed ========

    [Fact]
    public void MarkFailed_ShouldTransitionToFailed()
    {
        var refund = Refund.Create(Guid.NewGuid(), 50m, "EUR", DateTimeOffset.UtcNow);

        refund.MarkFailed();

        refund.Status.ShouldBe(RefundStatus.Failed);
        refund.ProviderRefundId.ShouldBeNull();
        refund.CompletedAt.ShouldBeNull();
    }
}
