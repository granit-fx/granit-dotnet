using Granit.Payments.Domain;
using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests;

public sealed class StripeStatusMapperTests
{
    [Theory]
    [InlineData("succeeded", ProviderChargeStatus.Succeeded)]
    [InlineData("processing", ProviderChargeStatus.Processing)]
    [InlineData("requires_action", ProviderChargeStatus.RequiresAction)]
    [InlineData("requires_confirmation", ProviderChargeStatus.RequiresAction)]
    [InlineData("requires_payment_method", ProviderChargeStatus.Failed)]
    [InlineData("canceled", ProviderChargeStatus.Failed)]
    [InlineData("unknown_status", ProviderChargeStatus.Failed)]
    public void MapChargeStatus_ShouldMapCorrectly(string stripeStatus, ProviderChargeStatus expected) =>
        StripeStatusMapper.MapChargeStatus(stripeStatus).ShouldBe(expected);

    [Theory]
    [InlineData("succeeded", PaymentStatus.Succeeded)]
    [InlineData("processing", PaymentStatus.Processing)]
    [InlineData("requires_action", PaymentStatus.RequiresAction)]
    [InlineData("canceled", PaymentStatus.Canceled)]
    [InlineData("requires_payment_method", PaymentStatus.Failed)]
    public void MapPaymentStatus_ShouldMapCorrectly(string stripeStatus, PaymentStatus expected) =>
        StripeStatusMapper.MapPaymentStatus(stripeStatus).ShouldBe(expected);

    [Theory]
    [InlineData("succeeded", RefundStatus.Succeeded)]
    [InlineData("pending", RefundStatus.Pending)]
    [InlineData("failed", RefundStatus.Failed)]
    [InlineData("canceled", RefundStatus.Failed)]
    public void MapRefundStatus_ShouldMapCorrectly(string stripeStatus, RefundStatus expected) =>
        StripeStatusMapper.MapRefundStatus(stripeStatus).ShouldBe(expected);
}
