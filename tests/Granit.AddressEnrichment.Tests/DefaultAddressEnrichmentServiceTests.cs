using Granit.AddressDeliverability;
using Granit.AddressEnrichment.Internal;
using Granit.Domain.ValueObjects;
using Granit.Geocoding;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AddressEnrichment.Tests;

public sealed class DefaultAddressEnrichmentServiceTests
{
    private static readonly Address SampleAddress =
        Address.Create("Rue de la Loi 16", "Brussels", "1000", "BE");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnrichAsync_GeocodeHit_ProducesResolvedGeocoding()
    {
        IGeocodingService geocoding = StubGeocoder(
            new GeocodingResult(new GeoCoordinate(50.85, 4.35), GeocodeMatchPrecision.Rooftop, HouseNumber: "16"));
        DefaultAddressEnrichmentService sut = new(geocoding, TimeProvider.System, deliverabilityService: null);

        AddressEnrichmentResult result = await sut.EnrichAsync(SampleAddress, Ct);

        result.Geocoding.Status.ShouldBe(AddressGeocodingStatus.Resolved);
        result.Geocoding.Coordinate.ShouldBe(new GeoCoordinate(50.85, 4.35));
        result.Geocoding.HouseNumber.ShouldBe("16");
        result.Verification.ShouldBe(AddressVerification.Unverified);
    }

    [Fact]
    public async Task EnrichAsync_GeocodeMiss_ProducesFailedGeocoding()
    {
        DefaultAddressEnrichmentService sut = new(StubGeocoder(null), TimeProvider.System, deliverabilityService: null);

        AddressEnrichmentResult result = await sut.EnrichAsync(SampleAddress, Ct);

        result.Geocoding.Status.ShouldBe(AddressGeocodingStatus.Failed);
        result.Geocoding.Coordinate.ShouldBeNull();
    }

    [Theory]
    [InlineData(AddressDeliverabilityOutcome.Verified, AddressVerificationStatus.ProviderVerified)]
    [InlineData(AddressDeliverabilityOutcome.Corrected, AddressVerificationStatus.Corrected)]
    [InlineData(AddressDeliverabilityOutcome.Invalid, AddressVerificationStatus.Invalid)]
    [InlineData(AddressDeliverabilityOutcome.Unverifiable, AddressVerificationStatus.Unverified)]
    public async Task EnrichAsync_WithProvider_MapsOutcomeToStatus(
        AddressDeliverabilityOutcome outcome, AddressVerificationStatus expected)
    {
        IAddressDeliverabilityService deliverability = Substitute.For<IAddressDeliverabilityService>();
        deliverability.CheckAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(new AddressDeliverabilityResult(outcome, ProviderMatchCode: "X1"));
        DefaultAddressEnrichmentService sut = new(StubGeocoder(null), TimeProvider.System, deliverability);

        AddressEnrichmentResult result = await sut.EnrichAsync(SampleAddress, Ct);

        result.Verification.Status.ShouldBe(expected);
        if (expected != AddressVerificationStatus.Unverified)
        {
            result.Verification.Source.ShouldBe(AddressVerificationSource.VerificationProvider);
            result.Verification.Evidence.ShouldBe("X1");
        }
    }

    private static IGeocodingService StubGeocoder(GeocodingResult? result)
    {
        IGeocodingService geocoding = Substitute.For<IGeocodingService>();
        geocoding.GeocodeAsync(Arg.Any<PostalAddress>(), Arg.Any<CancellationToken>()).Returns(result);
        return geocoding;
    }
}
