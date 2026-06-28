using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
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

    public async Task<GeoPoint?> ResolveAsync(PostalAddress address, CancellationToken cancellationToken = default)
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

            GeoPoint? point = Map(places);
            if (point is null && places is { Count: > 0 })
            {
                // A result was returned but its coordinate was unparseable or out of WGS 84 range — treat the
                // untrusted response as a miss rather than surfacing a bogus coordinate.
                LogLookupFailed("InvalidCoordinate");
            }

            return point;
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
        parameters.Add("addressdetails=0");
        return string.Join('&', parameters);
    }

    private static void AppendIfPresent(List<string> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private static GeoPoint? Map(List<NominatimPlace>? places)
    {
        if (places is not { Count: > 0 })
        {
            return null;
        }

        NominatimPlace first = places[0];
        if (double.TryParse(first.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
            && double.TryParse(first.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double lon)
            && lat is >= -90 and <= 90
            && lon is >= -180 and <= 180)
        {
            return new GeoPoint(lat, lon);
        }

        return null;
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
