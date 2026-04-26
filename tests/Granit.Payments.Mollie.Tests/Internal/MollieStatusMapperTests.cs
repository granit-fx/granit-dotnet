using Granit.Payments.Domain;
using Granit.Payments.Mollie.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Mollie.Tests.Internal;

public sealed class MollieStatusMapperTests
{
    [Theory]
    [InlineData("paid", ProviderChargeStatus.Succeeded)]
    [InlineData("authorized", ProviderChargeStatus.Succeeded)]
    [InlineData("pending", ProviderChargeStatus.Processing)]
    [InlineData("open", ProviderChargeStatus.RequiresAction)]
    [InlineData("expired", ProviderChargeStatus.Failed)]
    [InlineData("canceled", ProviderChargeStatus.Failed)]
    [InlineData("unknown", ProviderChargeStatus.Failed)]
    public void MapChargeStatus_ReturnsExpected(string status, ProviderChargeStatus expected) =>
        MollieStatusMapper.MapChargeStatus(status).ShouldBe(expected);

    [Theory]
    [InlineData("paid", PaymentStatus.Succeeded)]
    [InlineData("authorized", PaymentStatus.Succeeded)]
    [InlineData("pending", PaymentStatus.Processing)]
    [InlineData("open", PaymentStatus.RequiresAction)]
    [InlineData("canceled", PaymentStatus.Canceled)]
    [InlineData("expired", PaymentStatus.Failed)]
    [InlineData("failed", PaymentStatus.Failed)]
    [InlineData("unknown", PaymentStatus.Failed)]
    public void MapPaymentStatus_ReturnsExpected(string status, PaymentStatus expected) =>
        MollieStatusMapper.MapPaymentStatus(status).ShouldBe(expected);

    [Theory]
    [InlineData("refunded", RefundStatus.Succeeded)]
    [InlineData("pending", RefundStatus.Pending)]
    [InlineData("processing", RefundStatus.Pending)]
    [InlineData("queued", RefundStatus.Pending)]
    [InlineData("failed", RefundStatus.Failed)]
    [InlineData("unknown", RefundStatus.Failed)]
    public void MapRefundStatus_ReturnsExpected(string status, RefundStatus expected) =>
        MollieStatusMapper.MapRefundStatus(status).ShouldBe(expected);
}
