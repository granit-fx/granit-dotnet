using System.Net.Http.Json;
using System.Text.Json;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Photon.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Photon.Internal;

/// <summary>
/// Opt-in <see cref="IGeocodingProvider"/> backed by the Photon (<c>photon.komoot.io</c>) <c>/api</c> endpoint.
/// </summary>
/// <remarks>
/// Photon offers no structured-address parameters, so the components are joined into a single free-text <c>q</c>
/// query issued over a plain <see cref="HttpClient"/>; the first GeoJSON feature is parsed with
/// <c>System.Text.Json</c> — no external SDK. Outbound requests are serialised through a throttle that paces them to
/// <see cref="PhotonGeocodingOptions.RateLimitPerSecond"/>, honouring komoot's fair-use policy. Failures are caught
/// here and the address is never logged.
/// </remarks>
internal sealed partial class PhotonGeocodingProvider : IGeocodingProvider, IDisposable
{
    internal const string HttpClientName = "Granit.Geocoding.Photon";

    // Upper bound on a parsed address component persisted downstream (hardening against oversized OSM fields).
    // Aligned with the AddressGeocoding flat-column width (MapAddressGeocoding maps HouseNumber to varchar(32)).
    private const int MaxComponentLength = 32;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<PhotonGeocodingOptions> _options;
    private readonly ILogger<PhotonGeocodingProvider> _logger;
    private readonly TimeProvider _timeProvider;

    // Paces request starts to RateLimitPerSecond. The gate serialises the slot computation so concurrent callers
    // queue and each waits for its own future slot.
    private readonly SemaphoreSlim _throttleGate = new(1, 1);
    private DateTimeOffset _nextSlot = DateTimeOffset.MinValue;

    public PhotonGeocodingProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<PhotonGeocodingOptions> options,
        ILogger<PhotonGeocodingProvider> logger,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public string ProviderName => _options.Value.ProviderName;

    public void Dispose() => _throttleGate.Dispose();

    public async Task<GeocodingResult?> ResolveAsync(PostalAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        string query = BuildQuery(address, _options.Value.Language);
        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);

            using HttpRequestMessage request = new(HttpMethod.Get, $"api?{query}");
            using HttpResponseMessage response =
                await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRequestUnsuccessful((int)response.StatusCode);
                return null;
            }

            PhotonResponse? body = await response.Content
                .ReadFromJsonAsync<PhotonResponse>(cancellationToken)
                .ConfigureAwait(false);

            GeocodingResult? result = Map(body);
            if (result is null && body?.Features is { Count: > 0 })
            {
                // A result was returned but its coordinate was missing or out of WGS 84 range — treat the
                // untrusted response as a miss rather than surfacing a bogus coordinate.
                LogLookupFailed("InvalidCoordinate");
            }

            return result;
        }
        catch (HttpRequestException)
        {
            LogLookupFailed(nameof(HttpRequestException));
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogLookupFailed("Timeout");
            return null;
        }
        catch (JsonException)
        {
            LogLookupFailed(nameof(JsonException));
            return null;
        }
        catch (NotSupportedException)
        {
            LogLookupFailed(nameof(NotSupportedException));
            return null;
        }
    }

    private static string BuildQuery(PostalAddress address, string? language)
    {
        // Photon takes a single free-text query; join the present components so the more specific ones lead. limit=1
        // returns the single best match as a GeoJSON feature.
        List<string> components = [];
        AppendIfPresent(components, address.Street);
        AppendIfPresent(components, address.PostalCode);
        AppendIfPresent(components, address.Locality);
        AppendIfPresent(components, address.Country);

        List<string> parameters =
        [
            $"q={Uri.EscapeDataString(string.Join(", ", components))}",
            "limit=1",
        ];
        if (!string.IsNullOrWhiteSpace(language))
        {
            parameters.Add($"lang={Uri.EscapeDataString(language.Trim())}");
        }

        return string.Join('&', parameters);
    }

    private static void AppendIfPresent(List<string> components, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            components.Add(value.Trim());
        }
    }

    private static GeocodingResult? Map(PhotonResponse? response)
    {
        if (response?.Features is not { Count: > 0 })
        {
            return null;
        }

        PhotonFeature feature = response.Features[0];

        // Untrusted external input: GeoJSON orders coordinates [longitude, latitude]. TryCreate returns null for a
        // missing or out-of-range pair, which ResolveAsync reports as a miss.
        IReadOnlyList<double>? coordinates = feature.Geometry?.Coordinates;
        if (coordinates is not { Count: >= 2 }
            || GeoCoordinate.TryCreate(coordinates[1], coordinates[0]) is not { } coordinate)
        {
            return null;
        }

        PhotonProperties? properties = feature.Properties;
        return new GeocodingResult(
            coordinate,
            DeterminePrecision(properties),
            HouseNumber: Sanitize(properties?.HouseNumber),
            PostalCode: Sanitize(properties?.Postcode),
            CountryCode: Sanitize(properties?.CountryCode));
    }

    // Photon's feature type carries the granularity: a house number (or type "house") is rooftop, "street" is
    // street, everything else (locality/city/district/region/country) is locality.
    private static GeocodeMatchPrecision DeterminePrecision(PhotonProperties? properties)
    {
        if (!string.IsNullOrWhiteSpace(properties?.HouseNumber))
        {
            return GeocodeMatchPrecision.Rooftop;
        }

        return properties?.Type switch
        {
            "house" => GeocodeMatchPrecision.Rooftop,
            "street" => GeocodeMatchPrecision.Street,
            _ => GeocodeMatchPrecision.Locality,
        };
    }

    // Hardening: provider responses are untrusted input we persist downstream — trim and bound the length.
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= MaxComponentLength ? trimmed : trimmed[..MaxComponentLength];
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        double rate = _options.Value.RateLimitPerSecond;
        TimeSpan interval = rate > 0 ? TimeSpan.FromSeconds(1.0 / rate) : TimeSpan.Zero;

        await _throttleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (_nextSlot > now)
            {
                await Task.Delay(_nextSlot - now, _timeProvider, cancellationToken).ConfigureAwait(false);
            }

            _nextSlot = _timeProvider.GetUtcNow() + interval;
        }
        finally
        {
            _throttleGate.Release();
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Photon geocoding request returned status {StatusCode}.")]
    private partial void LogRequestUnsuccessful(int statusCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Photon geocoding lookup failed ({Reason}).")]
    private partial void LogLookupFailed(string reason);
}
