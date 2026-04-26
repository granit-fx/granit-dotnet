using Granit.Payments.Domain;
using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests.Internal;

public sealed class StripePaymentMethodTypeMapperTests
{
    [Theory]
    [InlineData(PaymentMethods.Card, "card")]
    [InlineData(PaymentMethods.SepaDebit, "sepa_debit")]
    [InlineData(PaymentMethods.Ideal, "ideal")]
    [InlineData(PaymentMethods.Bancontact, "bancontact")]
    [InlineData(PaymentMethods.BankTransfer, "customer_balance")]
    [InlineData(PaymentMethods.Klarna, "klarna")]
    [InlineData(PaymentMethods.Eps, "eps")]
    [InlineData(PaymentMethods.Giropay, "giropay")]
    [InlineData(PaymentMethods.PayPal, "paypal")]
    public void ToStripeTypes_KnownMethod_MapsToSingleStripeType(string granit, string stripe)
    {
        List<string> result = StripePaymentMethodTypeMapper.ToStripeTypes(granit);

        result.Count.ShouldBe(1);
        result[0].ShouldBe(stripe);
    }

    [Theory]
    [InlineData(PaymentMethods.ApplePay)]
    [InlineData(PaymentMethods.GooglePay)]
    public void ToStripeTypes_WalletMethods_MapToCard(string wallet)
    {
        List<string> result = StripePaymentMethodTypeMapper.ToStripeTypes(wallet);

        result.ShouldHaveSingleItem();
        result[0].ShouldBe("card");
    }

    [Fact]
    public void ToStripeTypes_UnknownMethod_FallsBackToCard() =>
        StripePaymentMethodTypeMapper.ToStripeTypes("unknown-method")[0].ShouldBe("card");

    [Theory]
    [InlineData("card", PaymentMethods.Card)]
    [InlineData("sepa_debit", PaymentMethods.SepaDebit)]
    [InlineData("ideal", PaymentMethods.Ideal)]
    [InlineData("bancontact", PaymentMethods.Bancontact)]
    [InlineData("customer_balance", PaymentMethods.BankTransfer)]
    [InlineData("klarna", PaymentMethods.Klarna)]
    [InlineData("eps", PaymentMethods.Eps)]
    [InlineData("giropay", PaymentMethods.Giropay)]
    [InlineData("paypal", PaymentMethods.PayPal)]
    public void FromStripeType_KnownStripeType_MapsToGranit(string stripe, string granit) =>
        StripePaymentMethodTypeMapper.FromStripeType(stripe).ShouldBe(granit);

    [Fact]
    public void FromStripeType_UnknownType_PassesThrough() =>
        StripePaymentMethodTypeMapper.FromStripeType("future_method").ShouldBe("future_method");
}
