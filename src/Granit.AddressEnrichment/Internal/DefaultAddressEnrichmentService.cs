using Granit.AddressDeliverability;
using Granit.Domain.ValueObjects;
using Granit.Geocoding;
using Granit.Geocoding.Extensions;

namespace Granit.AddressEnrichment.Internal;

/// <summary>
/// Default <see cref="IAddressEnrichmentService"/>: maps a <see cref="GeocodingResult"/> from
/// <see cref="IGeocodingService"/> to an <see cref="AddressGeocoding"/>, and — when an
/// <see cref="IAddressDeliverabilityService"/> is registered — an <see cref="AddressDeliverabilityResult"/> to
/// an <see cref="AddressVerification"/> verdict. Deliverability is skipped (verdict
/// <see cref="AddressVerification.Unverified"/>) when no provider is registered. The status-mapping lives
/// here so every consumer enriches identically.
/// </summary>
internal sealed class DefaultAddressEnrichmentService(
    IGeocodingService geocodingService,
    TimeProvider timeProvider,
    IAddressDeliverabilityService? deliverabilityService) : IAddressEnrichmentService
{
    public async Task<AddressEnrichmentResult> EnrichAsync(
        Address address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        DateTimeOffset now = timeProvider.GetUtcNow();

        GeocodingResult? geocode = await geocodingService
            .GeocodeAsync(address.ToPostalAddress(), cancellationToken)
            .ConfigureAwait(false);

        AddressGeocoding geocoding = geocode is null
            ? AddressGeocoding.Failed(now)
            : AddressGeocoding.Resolved(geocode.Coordinate, geocode.Precision, now, geocode.HouseNumber);

        AddressVerification verification = AddressVerification.Unverified;
        if (deliverabilityService is not null)
        {
            AddressDeliverabilityResult result = await deliverabilityService
                .CheckAsync(address, cancellationToken)
                .ConfigureAwait(false);
            verification = MapVerification(result, now);
        }

        return new AddressEnrichmentResult(geocoding, verification);
    }

    private static AddressVerification MapVerification(AddressDeliverabilityResult result, DateTimeOffset at)
    {
        AddressVerificationStatus status = result.Outcome switch
        {
            AddressDeliverabilityOutcome.Verified => AddressVerificationStatus.ProviderVerified,
            AddressDeliverabilityOutcome.Corrected => AddressVerificationStatus.Corrected,
            AddressDeliverabilityOutcome.Invalid => AddressVerificationStatus.Invalid,
            _ => AddressVerificationStatus.Unverified,
        };

        return status == AddressVerificationStatus.Unverified
            ? AddressVerification.Unverified
            : AddressVerification.Create(
                status,
                AddressVerificationSource.VerificationProvider,
                at,
                evidence: result.ProviderMatchCode);
    }
}
