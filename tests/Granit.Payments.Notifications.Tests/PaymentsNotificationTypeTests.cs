using Granit.Notifications;
using Shouldly;
using Xunit;

namespace Granit.Payments.Notifications.Tests;

public sealed class PaymentsNotificationTypeTests
{
    [Fact]
    public void PaymentSucceeded_ShouldHaveCorrectMetadata()
    {
        PaymentSucceededNotificationType type = PaymentSucceededNotificationType.Instance;

        type.Name.ShouldBe("Payments.PaymentSucceeded");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Success);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void PaymentSucceeded_ShouldBeSingleton()
    {
        PaymentSucceededNotificationType.Instance
            .ShouldBeSameAs(PaymentSucceededNotificationType.Instance);
    }

    [Fact]
    public void PaymentSucceededData_ShouldCreateRecord()
    {
        var transactionId = Guid.NewGuid();
        DateTimeOffset succeededAt = DateTimeOffset.UtcNow;

        var data = new PaymentSucceededNotificationData(
            transactionId, 99.99m, "EUR", "Stripe", succeededAt);

        data.TransactionId.ShouldBe(transactionId);
        data.Amount.ShouldBe(99.99m);
        data.Currency.ShouldBe("EUR");
        data.ProviderName.ShouldBe("Stripe");
        data.SucceededAt.ShouldBe(succeededAt);
    }

    [Fact]
    public void PaymentFailed_ShouldHaveCorrectMetadata()
    {
        PaymentFailedNotificationType type = PaymentFailedNotificationType.Instance;

        type.Name.ShouldBe("Payments.PaymentFailed");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Warning);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void PaymentFailed_ShouldBeSingleton()
    {
        PaymentFailedNotificationType.Instance
            .ShouldBeSameAs(PaymentFailedNotificationType.Instance);
    }

    [Fact]
    public void PaymentFailedData_ShouldCreateRecord()
    {
        var transactionId = Guid.NewGuid();

        var data = new PaymentFailedNotificationData(
            transactionId, 49.99m, "USD", "card_declined", "Stripe");

        data.TransactionId.ShouldBe(transactionId);
        data.Amount.ShouldBe(49.99m);
        data.Currency.ShouldBe("USD");
        data.FailureCode.ShouldBe("card_declined");
        data.ProviderName.ShouldBe("Stripe");
    }

    [Fact]
    public void PaymentFailedData_ShouldAllowNullFailureCode()
    {
        var data = new PaymentFailedNotificationData(
            Guid.NewGuid(), 25.00m, "EUR", null, "Stripe");

        data.FailureCode.ShouldBeNull();
    }

    [Fact]
    public void PaymentMethodExpiring_ShouldHaveCorrectMetadata()
    {
        PaymentMethodExpiringNotificationType type = PaymentMethodExpiringNotificationType.Instance;

        type.Name.ShouldBe("Payments.PaymentMethodExpiring");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Warning);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void PaymentMethodExpiring_ShouldBeSingleton()
    {
        PaymentMethodExpiringNotificationType.Instance
            .ShouldBeSameAs(PaymentMethodExpiringNotificationType.Instance);
    }

    [Fact]
    public void PaymentMethodExpiringData_ShouldCreateRecord()
    {
        var paymentMethodId = Guid.NewGuid();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(14);

        var data = new PaymentMethodExpiringNotificationData(
            paymentMethodId, "Visa ****4242", expiresAt, 14);

        data.PaymentMethodId.ShouldBe(paymentMethodId);
        data.DisplayLabel.ShouldBe("Visa ****4242");
        data.ExpiresAt.ShouldBe(expiresAt);
        data.DaysRemaining.ShouldBe(14);
    }

    [Fact]
    public void RefundProcessed_ShouldHaveCorrectMetadata()
    {
        RefundProcessedNotificationType type = RefundProcessedNotificationType.Instance;

        type.Name.ShouldBe("Payments.RefundProcessed");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Success);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void RefundProcessed_ShouldBeSingleton()
    {
        RefundProcessedNotificationType.Instance
            .ShouldBeSameAs(RefundProcessedNotificationType.Instance);
    }

    [Fact]
    public void RefundProcessedData_ShouldCreateRecord()
    {
        var transactionId = Guid.NewGuid();
        var refundId = Guid.NewGuid();

        var data = new RefundProcessedNotificationData(
            transactionId, refundId, 30.00m, "EUR", "Customer request");

        data.TransactionId.ShouldBe(transactionId);
        data.RefundId.ShouldBe(refundId);
        data.Amount.ShouldBe(30.00m);
        data.Currency.ShouldBe("EUR");
        data.Reason.ShouldBe("Customer request");
    }

    [Fact]
    public void RefundProcessedData_ShouldAllowNullReason()
    {
        var data = new RefundProcessedNotificationData(
            Guid.NewGuid(), Guid.NewGuid(), 10.00m, "EUR", null);

        data.Reason.ShouldBeNull();
    }

    [Fact]
    public void DisputeOpened_ShouldHaveCorrectMetadata()
    {
        DisputeOpenedNotificationType type = DisputeOpenedNotificationType.Instance;

        type.Name.ShouldBe("Payments.DisputeOpened");
        type.DefaultSeverity.ShouldBe(NotificationSeverity.Error);
        type.DefaultChannels.ShouldContain(NotificationChannels.Email);
        type.DefaultChannels.ShouldContain(NotificationChannels.InApp);
        type.DefaultChannels.Count.ShouldBe(2);
    }

    [Fact]
    public void DisputeOpened_ShouldBeSingleton()
    {
        DisputeOpenedNotificationType.Instance
            .ShouldBeSameAs(DisputeOpenedNotificationType.Instance);
    }

    [Fact]
    public void DisputeOpenedData_ShouldCreateRecord()
    {
        var transactionId = Guid.NewGuid();
        var disputeId = Guid.NewGuid();

        var data = new DisputeOpenedNotificationData(
            transactionId, disputeId, 150.00m, "EUR", "Unauthorized transaction");

        data.TransactionId.ShouldBe(transactionId);
        data.DisputeId.ShouldBe(disputeId);
        data.Amount.ShouldBe(150.00m);
        data.Currency.ShouldBe("EUR");
        data.Reason.ShouldBe("Unauthorized transaction");
    }
}
