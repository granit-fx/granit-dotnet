using System.Collections.Immutable;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Payments.Internal;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Internal;

public sealed class DefaultPaymentMethodAvailabilityFilterTests
{
    private static readonly PaymentMethodCapability Wildcard = new(
        SupportedCountries: ImmutableHashSet<string>.Empty,
        SupportedCurrencies: ImmutableHashSet<string>.Empty,
        SupportedSequenceTypes: PaymentMethodSequenceTypes.OneOff
            | PaymentMethodSequenceTypes.First
            | PaymentMethodSequenceTypes.Recurring,
        AmountBounds: ImmutableDictionary<string, PaymentMethodAmountBound>.Empty);

    private readonly DefaultPaymentMethodAvailabilityFilter _filter = new();

    [Fact]
    public void Wildcards_AlwaysAvailable() =>
        _filter.IsAvailable(Wildcard, new PaymentAvailabilityContext("BE", "EUR", 25m)).ShouldBeTrue();

    [Fact]
    public void EmptyContext_IgnoresAllAxes() =>
        _filter.IsAvailable(
                Wildcard,
                new PaymentAvailabilityContext(null, null, null, PaymentMethodSequenceTypes.None))
            .ShouldBeTrue();

    [Fact]
    public void Country_OutsideSupportedSet_Rejected()
    {
        PaymentMethodCapability bancontact = Wildcard with { SupportedCountries = ImmutableHashSet.Create("BE") };

        _filter.IsAvailable(bancontact, new PaymentAvailabilityContext("NL", null, null)).ShouldBeFalse();
    }

    [Fact]
    public void Country_InsideSupportedSet_Accepted()
    {
        PaymentMethodCapability bancontact = Wildcard with { SupportedCountries = ImmutableHashSet.Create("BE") };

        _filter.IsAvailable(bancontact, new PaymentAvailabilityContext("BE", null, null)).ShouldBeTrue();
    }

    [Fact]
    public void Country_NullContext_TreatedAsWildcard()
    {
        PaymentMethodCapability bancontact = Wildcard with { SupportedCountries = ImmutableHashSet.Create("BE") };

        _filter.IsAvailable(bancontact, new PaymentAvailabilityContext(null, null, null)).ShouldBeTrue();
    }

    [Fact]
    public void Currency_OutsideSupportedSet_Rejected()
    {
        PaymentMethodCapability sepa = Wildcard with { SupportedCurrencies = ImmutableHashSet.Create("EUR") };

        _filter.IsAvailable(sepa, new PaymentAvailabilityContext(null, "USD", null)).ShouldBeFalse();
    }

    [Fact]
    public void SequenceType_RequestedRecurring_AgainstOneOffOnly_Rejected()
    {
        PaymentMethodCapability oneOffOnly = Wildcard with { SupportedSequenceTypes = PaymentMethodSequenceTypes.OneOff };

        _filter.IsAvailable(
                oneOffOnly,
                new PaymentAvailabilityContext(null, null, null, PaymentMethodSequenceTypes.Recurring))
            .ShouldBeFalse();
    }

    [Fact]
    public void SequenceType_RequestedRecurring_AgainstFirstAndRecurring_Accepted()
    {
        PaymentMethodCapability sepa = Wildcard with
        {
            SupportedSequenceTypes = PaymentMethodSequenceTypes.First | PaymentMethodSequenceTypes.Recurring,
        };

        _filter.IsAvailable(
                sepa,
                new PaymentAvailabilityContext(null, null, null, PaymentMethodSequenceTypes.Recurring))
            .ShouldBeTrue();
    }

    [Fact]
    public void SequenceType_RequestedFirst_AgainstOneOffAndFirst_Accepted()
    {
        PaymentMethodCapability bancontact = Wildcard with
        {
            SupportedSequenceTypes = PaymentMethodSequenceTypes.OneOff | PaymentMethodSequenceTypes.First,
        };

        _filter.IsAvailable(
                bancontact,
                new PaymentAvailabilityContext(null, null, null, PaymentMethodSequenceTypes.First))
            .ShouldBeTrue();
    }

    [Fact]
    public void SequenceType_None_BypassesCheck()
    {
        PaymentMethodCapability sepa = Wildcard with
        {
            SupportedSequenceTypes = PaymentMethodSequenceTypes.First | PaymentMethodSequenceTypes.Recurring,
        };

        _filter.IsAvailable(
                sepa,
                new PaymentAvailabilityContext(null, null, null, PaymentMethodSequenceTypes.None))
            .ShouldBeTrue();
    }

    [Fact]
    public void Amount_BelowMinimum_Rejected()
    {
        PaymentMethodCapability klarna = Wildcard with
        {
            AmountBounds = new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", MinAmount: 10m, MaxAmount: 10_000m),
            },
        };

        _filter.IsAvailable(klarna, new PaymentAvailabilityContext(null, "EUR", 5m)).ShouldBeFalse();
    }

    [Fact]
    public void Amount_AboveMaximum_Rejected()
    {
        PaymentMethodCapability klarna = Wildcard with
        {
            AmountBounds = new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", MinAmount: 10m, MaxAmount: 10_000m),
            },
        };

        _filter.IsAvailable(klarna, new PaymentAvailabilityContext(null, "EUR", 12_000m)).ShouldBeFalse();
    }

    [Fact]
    public void Amount_WithinBounds_Accepted()
    {
        PaymentMethodCapability klarna = Wildcard with
        {
            AmountBounds = new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", MinAmount: 10m, MaxAmount: 10_000m),
            },
        };

        _filter.IsAvailable(klarna, new PaymentAvailabilityContext(null, "EUR", 25m)).ShouldBeTrue();
    }

    [Fact]
    public void Amount_CurrencyMismatch_NoBoundApplied()
    {
        PaymentMethodCapability klarnaEurOnly = Wildcard with
        {
            AmountBounds = new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", MinAmount: 10m, MaxAmount: 10_000m),
            },
        };

        // Request in GBP — no bound defined for GBP, filter does not enforce
        _filter.IsAvailable(klarnaEurOnly, new PaymentAvailabilityContext(null, "GBP", 5m)).ShouldBeTrue();
    }

    [Fact]
    public void Amount_OnlyMinSet_NoCeiling()
    {
        PaymentMethodCapability method = Wildcard with
        {
            AmountBounds = new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", MinAmount: 10m, MaxAmount: null),
            },
        };

        _filter.IsAvailable(method, new PaymentAvailabilityContext(null, "EUR", 9m)).ShouldBeFalse();
        _filter.IsAvailable(method, new PaymentAvailabilityContext(null, "EUR", 1_000_000m)).ShouldBeTrue();
    }

    [Fact]
    public void AllAxes_IntersectionRejectsOnAnyFailure()
    {
        PaymentMethodCapability method = new(
            SupportedCountries: ImmutableHashSet.Create("BE", "NL"),
            SupportedCurrencies: ImmutableHashSet.Create("EUR"),
            SupportedSequenceTypes: PaymentMethodSequenceTypes.OneOff,
            AmountBounds: new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", 1m, 500m),
            });

        // All axes OK
        _filter.IsAvailable(method, new PaymentAvailabilityContext("BE", "EUR", 50m)).ShouldBeTrue();

        // Country fails
        _filter.IsAvailable(method, new PaymentAvailabilityContext("DE", "EUR", 50m)).ShouldBeFalse();

        // Currency fails
        _filter.IsAvailable(method, new PaymentAvailabilityContext("BE", "USD", 50m)).ShouldBeFalse();

        // Amount fails
        _filter.IsAvailable(method, new PaymentAvailabilityContext("BE", "EUR", 1000m)).ShouldBeFalse();

        // Sequence fails
        _filter.IsAvailable(
                method,
                new PaymentAvailabilityContext("BE", "EUR", 50m, PaymentMethodSequenceTypes.Recurring))
            .ShouldBeFalse();
    }
}
