using Granit.AddressEnrichment.Extensions;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AddressEnrichment.Tests;

public sealed class AddressEnrichmentServiceExtensionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnrichAsync_AppliesServiceResultToHolder()
    {
        var geocoding = AddressGeocoding.Resolved(
            new GeoCoordinate(50.85, 4.35), GeocodeMatchPrecision.Rooftop, DateTimeOffset.UnixEpoch);
        var verification = AddressVerification.Create(
            AddressVerificationStatus.ProviderVerified,
            AddressVerificationSource.VerificationProvider,
            DateTimeOffset.UnixEpoch);

        IAddressEnrichmentService service = Substitute.For<IAddressEnrichmentService>();
        TestHolder holder = new(Address.Create("Rue de la Loi 16", "Brussels", "1000", "BE"));
        service.EnrichAsync(holder.Address, Arg.Any<CancellationToken>())
            .Returns(new AddressEnrichmentResult(geocoding, verification));

        await service.EnrichAsync(holder, Ct);

        holder.Geocoding.ShouldBe(geocoding);
        holder.Verification.ShouldBe(verification);
    }

    private sealed class TestHolder(Address address) : IGeoEnrichedAddress
    {
        public Address Address { get; } = address;
        public AddressGeocoding Geocoding { get; private set; } = AddressGeocoding.Pending;
        public AddressVerification Verification { get; private set; } = AddressVerification.Unverified;

        public void ApplyEnrichment(AddressGeocoding geocoding, AddressVerification verification)
        {
            Geocoding = geocoding;
            Verification = verification;
        }
    }
}
