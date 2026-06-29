using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Nominatim.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Nominatim.Internal;

/// <summary>
/// Opt-in <see cref="IGeocodingProvider"/> backed by the OpenStreetMap Nominatim <c>/search</c> API.
/// </summary>
/// <remarks>
/// Issues a structured query (<c>street</c>/<c>postalcode</c>/<c>city</c>/<c>country</c>) over a plain
/// <see cref="HttpClient"/> and parses the first result with <c>System.Text.Json</c> — no external SDK. Outbound
/// requests are serialised through a throttle that paces them to <see cref="NominatimGeocodingOptions.RateLimitPerSecond"/>,
/// honouring the public endpoint's 1 req/s policy. Failures are caught here and the address is never logged.
/// </remarks>
internal sealed partial class NominatimGeocodingProvider : IGeocodingProvider, IDisposable
{
    internal const string HttpClientName = "Granit.Geocoding.Nominatim";

    // Upper bound on a parsed address component persisted downstream (hardening against oversized OSM fields).
    // Aligned with the AddressGeocoding flat-column width (MapAddressGeocoding maps HouseNumber to varchar(32)).
    private const int MaxComponentLength = 32;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<NominatimGeocodingOptions> _options;
    private readonly ILogger<NominatimGeocodingProvider> _logger;
    private readonly TimeProvider _timeProvider;

    // Paces request starts to RateLimitPerSecond. The gate serialises the slot computation so concurrent callers
    // queue and each waits for its own future slot.
    private readonly SemaphoreSlim _throttleGate = new(1, 1);
    private DateTimeOffset _nextSlot = DateTimeOffset.MinValue;

    public NominatimGeocodingProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<NominatimGeocodingOptions> options,
        ILogger<NominatimGeocodingProvider> logger,
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

        string query = BuildQuery(address);
        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

        try
        {
            await ThrottleAsync(cancellationToken).ConfigureAwait(false);

            using HttpRequestMessage request = new(HttpMethod.Get, $"search?{query}");
            using HttpResponseMessage response =
                await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRequestUnsuccessful((int)response.StatusCode);
                return null;
            }

            List<NominatimPlace>? places = await response.Content
                .ReadFromJsonAsync<List<NominatimPlace>>(cancellationToken)
                .ConfigureAwait(false);

            GeocodingResult? result = Map(places);
            if (result is null && places is { Count: > 0 })
            {
                // A result was returned but its coordinate was unparseable or out of WGS 84 range — treat the
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

    private static string BuildQuery(PostalAddress address)
    {
        // Structured Nominatim query. Optional components are omitted when absent; jsonv2 + limit=1 returns the best
        // single match with lat/lon. addressdetails=0 keeps the payload minimal (we only need the coordinate).
        List<string> parameters = [];
        AppendIfPresent(parameters, "street", address.Street);
        AppendIfPresent(parameters, "postalcode", address.PostalCode);
        AppendIfPresent(parameters, "city", address.Locality);
        AppendIfPresent(parameters, "country", address.Country);
        parameters.Add("format=jsonv2");
        parameters.Add("limit=1");
        // addressdetails=1 returns the parsed address components (house_number, postcode, country_code) and,
        // with addresstype/place_rank, the match granularity — used for the result's precision and cross-check.
        parameters.Add("addressdetails=1");
        return string.Join('&', parameters);
    }

    private static void AppendIfPresent(List<string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static GeocodingResult? Map(List<NominatimPlace>? places)
    {
        if (places is not { Count: > 0 })
        {
            return null;
        }

        NominatimPlace first = places[0];

        // Untrusted external input: TryCreate returns null for an unparseable or out-of-range coordinate, which
        // ResolveAsync reports as a miss rather than surfacing a bogus point.
        if (!double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
            || !double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
            || GeoCoordinate.TryCreate(lat, lon) is not { } coordinate)
        {
            return null;
        }

        return new GeocodingResult(
            coordinate,
            DeterminePrecision(first),
            HouseNumber: Sanitize(first.Address?.HouseNumber),
            PostalCode: Sanitize(first.Address?.Postcode),
            CountryCode: Sanitize(first.Address?.CountryCode));
    }

    // Map the Nominatim granularity hints to a coarse precision: a parsed house number (or a house/building
    // address type) is rooftop; a road is street; everything else falls back to a place_rank threshold.
    private static GeocodeMatchPrecision DeterminePrecision(NominatimPlace place)
    {
        if (!string.IsNullOrWhiteSpace(place.Address?.HouseNumber))
        {
            return GeocodeMatchPrecision.Rooftop;
        }

        return place.AddressType switch
        {
            "house" or "building" or "residential" => GeocodeMatchPrecision.Rooftop,
            "road" or "street" or "pedestrian" or "footway" => GeocodeMatchPrecision.Street,
            _ => place.PlaceRank switch
            {
                >= 30 => GeocodeMatchPrecision.Rooftop,
                >= 26 => GeocodeMatchPrecision.Street,
                _ => GeocodeMatchPrecision.Locality,
            },
        };
    }

    // Hardening: provider responses are untrusted input we persist downstream — trim and bound the length so a
    // garbage / oversized OSM field cannot bloat a stored row.
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
        Message = "Nominatim geocoding request returned status {StatusCode}.")]
    private partial void LogRequestUnsuccessful(int statusCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Nominatim geocoding lookup failed ({Reason}).")]
    private partial void LogLookupFailed(string reason);
}
