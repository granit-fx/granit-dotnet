using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Endpoints.Tests.Internal;

public sealed class PaymentAvailabilityContextParserTests
{
    [Fact]
    public void AllAxesNull_ReturnsNullContextAndNoError()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: null, currency: null, amount: null, sequenceType: null, out string? error);

        context.ShouldBeNull();
        error.ShouldBeNull();
    }

    [Fact]
    public void Country_NormalizedToUppercase()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: "be", currency: null, amount: null, sequenceType: null, out string? error);

        error.ShouldBeNull();
        context!.CountryCode.ShouldBe("BE");
    }

    [Fact]
    public void Country_InvalidLength_Rejected()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: "BEL", currency: null, amount: null, sequenceType: null, out string? error);

        context.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("ISO-3166");
    }

    [Fact]
    public void Currency_InvalidLength_Rejected()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: null, currency: "EU", amount: null, sequenceType: null, out string? error);

        context.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("ISO-4217");
    }

    [Fact]
    public void Amount_Negative_Rejected()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: null, currency: null, amount: -1m, sequenceType: null, out string? error);

        context.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("non-negative");
    }

    [Theory]
    [InlineData("oneoff", PaymentMethodSequenceType.OneOff)]
    [InlineData("First", PaymentMethodSequenceType.First)]
    [InlineData("RECURRING", PaymentMethodSequenceType.Recurring)]
    public void SequenceType_CaseInsensitive(string input, PaymentMethodSequenceType expected)
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: null, currency: null, amount: null, sequenceType: input, out string? error);

        error.ShouldBeNull();
        context!.SequenceType.ShouldBe(expected);
    }

    [Fact]
    public void SequenceType_Unknown_Rejected()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: null, currency: null, amount: null, sequenceType: "weekly", out string? error);

        context.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("sequence type");
    }

    [Fact]
    public void AllAxesValid_ReturnsPopulatedContext()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: "BE", currency: "EUR", amount: 25m, sequenceType: "oneoff", out string? error);

        error.ShouldBeNull();
        context.ShouldNotBeNull();
        context!.CountryCode.ShouldBe("BE");
        context.CurrencyCode.ShouldBe("EUR");
        context.Amount.ShouldBe(25m);
        context.SequenceType.ShouldBe(PaymentMethodSequenceType.OneOff);
    }

    [Fact]
    public void SequenceTypeAbsent_DefaultsToOneOff_WhenOtherAxisSet()
    {
        PaymentAvailabilityContext? context = PaymentAvailabilityContextParser
            .TryParse(country: "BE", currency: null, amount: null, sequenceType: null, out string? error);

        error.ShouldBeNull();
        context!.SequenceType.ShouldBe(PaymentMethodSequenceType.OneOff);
    }
}
