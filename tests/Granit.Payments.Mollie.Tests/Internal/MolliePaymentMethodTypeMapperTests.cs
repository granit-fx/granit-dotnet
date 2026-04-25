using Granit.Payments.Domain;
using Granit.Payments.Mollie.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Mollie.Tests.Internal;

public sealed class MolliePaymentMethodTypeMapperTests
{
    [Theory]
    [InlineData(PaymentMethods.Card, "creditcard")]
    [InlineData(PaymentMethods.SepaDebit, "directdebit")]
    [InlineData(PaymentMethods.BankTransfer, "banktransfer")]
    [InlineData(PaymentMethods.Ideal, "ideal")]
    [InlineData(PaymentMethods.Bancontact, "bancontact")]
    [InlineData(PaymentMethods.Eps, "eps")]
    [InlineData(PaymentMethods.PayPal, "paypal")]
    [InlineData(PaymentMethods.Klarna, "klarna")]
    [InlineData(PaymentMethods.ApplePay, "applepay")]
    [InlineData(PaymentMethods.GooglePay, "googlepay")]
    public void ToMollieMethod_KnownMethod_MapsCorrectly(string granit, string expected) =>
        MolliePaymentMethodTypeMapper.ToMollieMethod(granit).ShouldBe(expected);

    [Fact]
    public void ToMollieMethod_UnknownMethod_ReturnsNull() =>
        MolliePaymentMethodTypeMapper.ToMollieMethod("unknown-method").ShouldBeNull();

    [Theory]
    [InlineData("creditcard", PaymentMethods.Card)]
    [InlineData("directdebit", PaymentMethods.SepaDebit)]
    [InlineData("sepadirectdebit", PaymentMethods.SepaDebit)] // alias
    [InlineData("banktransfer", PaymentMethods.BankTransfer)]
    [InlineData("ideal", PaymentMethods.Ideal)]
    [InlineData("paypal", PaymentMethods.PayPal)]
    [InlineData("klarna", PaymentMethods.Klarna)]
    public void FromMollieMethod_KnownString_MapsToGranit(string mollie, string expected) =>
        MolliePaymentMethodTypeMapper.FromMollieMethod(mollie).ShouldBe(expected);

    [Fact]
    public void FromMollieMethod_UnknownString_PassesThrough() =>
        MolliePaymentMethodTypeMapper.FromMollieMethod("future_method").ShouldBe("future_method");
}
