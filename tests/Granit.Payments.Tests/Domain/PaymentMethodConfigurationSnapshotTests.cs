using System.Collections.Immutable;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Shouldly;
using Xunit;

namespace Granit.Payments.Tests.Domain;

public sealed class PaymentMethodConfigurationSnapshotTests
{
    private static PaymentMethodConfiguration NewConfig() =>
        PaymentMethodConfiguration.Activate(Guid.NewGuid(), "mollie", "bancontact");

    [Fact]
    public void SnapshotCapability_WithAllWildcards_StoresNulls()
    {
        PaymentMethodConfiguration config = NewConfig();

        config.SnapshotCapability(PaymentMethodCapability.Wildcard);

        // Empty axes collapse to null; sequence marker is preserved
        config.SupportedCountries.ShouldBeNull();
        config.SupportedCurrencies.ShouldBeNull();
        config.AmountBounds.ShouldBeNull();
        config.SupportedSequenceTypes.ShouldNotBeNull();
    }

    [Fact]
    public void SnapshotCapability_WithPopulatedAxes_StoresValues()
    {
        PaymentMethodConfiguration config = NewConfig();
        PaymentMethodCapability capability = new(
            SupportedCountries: ImmutableHashSet.Create("BE", "NL"),
            SupportedCurrencies: ImmutableHashSet.Create("EUR"),
            SupportedSequenceTypes: PaymentMethodSequenceTypes.OneOff | PaymentMethodSequenceTypes.First,
            AmountBounds: new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", 1m, 10_000m),
            });

        config.SnapshotCapability(capability);

        config.SupportedCountries.ShouldBe(["BE", "NL"], ignoreOrder: true);
        config.SupportedCurrencies.ShouldBe(["EUR"]);
        config.AmountBounds.ShouldNotBeNull();
        config.AmountBounds["EUR"].MinAmount.ShouldBe(1m);
    }

    [Fact]
    public void GetCapabilitySnapshot_WithWildcardsPersisted_HydratesToEmptyCollections()
    {
        PaymentMethodConfiguration config = NewConfig();
        config.SnapshotCapability(PaymentMethodCapability.Wildcard);

        PaymentMethodCapability? snapshot = config.GetCapabilitySnapshot();

        snapshot.ShouldNotBeNull();
        snapshot!.SupportedCountries.Count.ShouldBe(0);
        snapshot.SupportedCurrencies.Count.ShouldBe(0);
        snapshot.AmountBounds.Count.ShouldBe(0);
        snapshot.SupportedSequenceTypes.ShouldBe(PaymentMethodCapability.Wildcard.SupportedSequenceTypes);
    }

    [Fact]
    public void GetCapabilitySnapshot_ReturnsNull_ForLegacyRecordWithoutSnapshot()
    {
        PaymentMethodConfiguration config = NewConfig();
        // Fresh Activate — never snapshotted

        PaymentMethodCapability? snapshot = config.GetCapabilitySnapshot();

        snapshot.ShouldBeNull();
    }

    [Fact]
    public void Snapshot_RoundTrip_PreservesValues()
    {
        PaymentMethodCapability original = new(
            SupportedCountries: ImmutableHashSet.Create("BE", "NL", "FR"),
            SupportedCurrencies: ImmutableHashSet.Create("EUR", "GBP"),
            SupportedSequenceTypes: PaymentMethodSequenceTypes.First | PaymentMethodSequenceTypes.Recurring,
            AmountBounds: new Dictionary<string, PaymentMethodAmountBound>
            {
                ["EUR"] = new("EUR", 10m, 10_000m),
                ["GBP"] = new("GBP", 8m, 8_000m),
            });

        PaymentMethodConfiguration config = NewConfig();
        config.SnapshotCapability(original);

        PaymentMethodCapability? roundTripped = config.GetCapabilitySnapshot();

        roundTripped.ShouldNotBeNull();
        roundTripped!.SupportedCountries.ShouldBe(original.SupportedCountries, ignoreOrder: true);
        roundTripped.SupportedCurrencies.ShouldBe(original.SupportedCurrencies, ignoreOrder: true);
        roundTripped.SupportedSequenceTypes.ShouldBe(original.SupportedSequenceTypes);
        roundTripped.AmountBounds["EUR"].MinAmount.ShouldBe(10m);
        roundTripped.AmountBounds["GBP"].MaxAmount.ShouldBe(8_000m);
    }

    [Fact]
    public void ClearCapability_ResetsAllAxesToNull()
    {
        PaymentMethodConfiguration config = NewConfig();
        config.SnapshotCapability(PaymentMethodCapability.Wildcard);

        config.ClearCapability();

        config.SupportedCountries.ShouldBeNull();
        config.SupportedCurrencies.ShouldBeNull();
        config.SupportedSequenceTypes.ShouldBeNull();
        config.AmountBounds.ShouldBeNull();
        config.GetCapabilitySnapshot().ShouldBeNull();
    }
}
