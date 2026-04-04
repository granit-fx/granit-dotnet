using Granit.Payments.Domain;
using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests;

public sealed class StripePaymentMethodTypeMapperTests
{
    [Theory]
    [InlineData(PaymentMethods.Card, "card")]
    [InlineData(PaymentMethods.SepaDebit, "sepa_debit")]
    [InlineData(PaymentMethods.Ideal, "ideal")]
    [InlineData(PaymentMethods.Bancontact, "bancontact")]
    [InlineData(PaymentMethods.BankTransfer, "customer_balance")]
    public void ToStripeTypes_ShouldReturnExpectedType(string input, string expected)
    {
        StripePaymentMethodTypeMapper.ToStripeTypes(input).ShouldContain(expected);
        StripePaymentMethodTypeMapper.ToStripeTypes(input).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("card", PaymentMethods.Card)]
    [InlineData("sepa_debit", PaymentMethods.SepaDebit)]
    [InlineData("ideal", PaymentMethods.Ideal)]
    [InlineData("bancontact", PaymentMethods.Bancontact)]
    [InlineData("customer_balance", PaymentMethods.BankTransfer)]
    public void FromStripeType_ShouldMapBack(string stripeType, string expected)
    {
        StripePaymentMethodTypeMapper.FromStripeType(stripeType).ShouldBe(expected);
    }

    [Fact]
    public void FromStripeType_UnknownType_ShouldPassThrough()
    {
        StripePaymentMethodTypeMapper.FromStripeType("unknown_type").ShouldBe("unknown_type");
    }
}
