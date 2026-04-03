using Granit.Payments.Domain;
using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests;

public sealed class StripePaymentMethodTypeMapperTests
{
    [Theory]
    [InlineData(PaymentMethodType.Card, "card")]
    [InlineData(PaymentMethodType.SepaDebit, "sepa_debit")]
    [InlineData(PaymentMethodType.Ideal, "ideal")]
    [InlineData(PaymentMethodType.Bancontact, "bancontact")]
    [InlineData(PaymentMethodType.BankTransfer, "customer_balance")]
    public void ToStripeTypes_ShouldReturnExpectedType(PaymentMethodType input, string expected)
    {
        StripePaymentMethodTypeMapper.ToStripeTypes(input).ShouldContain(expected);
        StripePaymentMethodTypeMapper.ToStripeTypes(input).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("card", PaymentMethodType.Card)]
    [InlineData("sepa_debit", PaymentMethodType.SepaDebit)]
    [InlineData("ideal", PaymentMethodType.Ideal)]
    [InlineData("bancontact", PaymentMethodType.Bancontact)]
    [InlineData("customer_balance", PaymentMethodType.BankTransfer)]
    public void FromStripeType_ShouldMapBack(string stripeType, PaymentMethodType expected)
    {
        StripePaymentMethodTypeMapper.FromStripeType(stripeType).ShouldBe(expected);
    }

    [Fact]
    public void FromStripeType_UnknownType_ShouldDefaultToCard()
    {
        StripePaymentMethodTypeMapper.FromStripeType("unknown_type").ShouldBe(PaymentMethodType.Card);
    }
}
