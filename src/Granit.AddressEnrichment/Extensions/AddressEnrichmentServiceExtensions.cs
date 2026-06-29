using Granit.Domain;

namespace Granit.AddressEnrichment.Extensions;

/// <summary>
/// Extension methods for <see cref="IAddressEnrichmentService"/>.
/// </summary>
public static class AddressEnrichmentServiceExtensions
{
    /// <summary>
    /// Enriches <paramref name="holder"/>'s address and applies the result back to it — the shared
    /// read-enrich-write-back step for any <see cref="IGeoEnrichedAddress"/>, so each address-holding module
    /// does not reimplement the mapping.
    /// </summary>
    /// <param name="service">The enrichment service.</param>
    /// <param name="holder">The address holder to enrich in place.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task EnrichAsync(
        this IAddressEnrichmentService service,
        IGeoEnrichedAddress holder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(holder);

        AddressEnrichmentResult result =
            await service.EnrichAsync(holder.Address, cancellationToken).ConfigureAwait(false);
        holder.ApplyEnrichment(result.Geocoding, result.Verification);
    }
}
