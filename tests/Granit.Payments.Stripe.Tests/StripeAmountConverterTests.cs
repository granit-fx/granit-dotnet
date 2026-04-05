using Granit.Payments.Stripe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Stripe.Tests;

public sealed class StripeAmountConverterTests
{
    [Theory]
    [InlineData(29.99, "EUR", 2999L)]
    [InlineData(100.00, "USD", 10000L)]
    [InlineData(0.50, "GBP", 50L)]
    [InlineData(1.00, "EUR", 100L)]
    [InlineData(0, "EUR", 0L)]
    public void ToStripeAmount_StandardCurrency_ShouldMultiplyBy100(decimal amount, string currency, long expected) =>
        StripeAmountConverter.ToStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData(1000, "JPY", 1000L)]
    [InlineData(5000, "KRW", 5000L)]
    [InlineData(250, "VND", 250L)]
    public void ToStripeAmount_ZeroDecimalCurrency_ShouldNotMultiply(decimal amount, string currency, long expected) =>
        StripeAmountConverter.ToStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData(2999L, "EUR", 29.99)]
    [InlineData(10000L, "USD", 100.00)]
    [InlineData(50L, "GBP", 0.50)]
    public void FromStripeAmount_StandardCurrency_ShouldDivideBy100(long amount, string currency, decimal expected) =>
        StripeAmountConverter.FromStripeAmount(amount, currency).ShouldBe(expected);

    [Theory]
    [InlineData(1000L, "JPY", 1000)]
    [InlineData(5000L, "KRW", 5000)]
    public void FromStripeAmount_ZeroDecimalCurrency_ShouldNotDivide(long amount, string currency, decimal expected)
    {
        StripeAmountConverter.FromStripeAmount(amount, currency).ShouldBe(expected);
    }

    [Fact]
    public void ToStripeAmount_CaseInsensitive_ShouldWork()
    {
        StripeAmountConverter.ToStripeAmount(1000m, "jpy").ShouldBe(1000L);
        StripeAmountConverter.ToStripeAmount(1000m, "JPY").ShouldBe(1000L);
    }

    [Fact]
    public void RoundTrip_StandardCurrency_ShouldPreserveValue()
    {
        decimal original = 42.50m;
        long stripe = StripeAmountConverter.ToStripeAmount(original, "EUR");
        decimal back = StripeAmountConverter.FromStripeAmount(stripe, "EUR");
        back.ShouldBe(original);
    }
}
