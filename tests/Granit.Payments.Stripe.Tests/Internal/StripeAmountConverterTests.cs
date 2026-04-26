using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests.Internal;

public sealed class StripeAmountConverterTests
{
    [Theory]
    [InlineData(10.00, "EUR", 1000L)]
    [InlineData(0.01, "USD", 1L)]
    [InlineData(100.50, "EUR", 10050L)]
    [InlineData(0.005, "EUR", 1L)] // rounds away from zero
    public void ToStripeAmount_DecimalCurrency_MultipliesByHundred(
        decimal amount, string currency, long expected) =>
        StripeAmountConverter.ToStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData(1234, "JPY", 1234L)]
    [InlineData(0, "KRW", 0L)]
    [InlineData(99, "VND", 99L)]
    public void ToStripeAmount_ZeroDecimalCurrency_KeepsValueAsIs(
        decimal amount, string currency, long expected) =>
        StripeAmountConverter.ToStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData(1000L, "EUR", 10.00)]
    [InlineData(1L, "USD", 0.01)]
    [InlineData(1234L, "JPY", 1234)]
    [InlineData(1234L, "KRW", 1234)]
    public void FromStripeAmount_RoundTripsCorrectly(long amount, string currency, decimal expected) =>
        StripeAmountConverter.FromStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData("eur")]
    [InlineData("EUR")]
    [InlineData("Eur")]
    public void ToStripeAmount_CurrencyMatchIsCaseInsensitive(string currency) =>
        StripeAmountConverter.ToStripeAmount(1.00m, currency).ShouldBe(100L);

    [Theory]
    [InlineData("jpy")]
    [InlineData("JPY")]
    public void ToStripeAmount_ZeroDecimalCurrencyMatchIsCaseInsensitive(string currency) =>
        StripeAmountConverter.ToStripeAmount(1234m, currency).ShouldBe(1234L);
}
