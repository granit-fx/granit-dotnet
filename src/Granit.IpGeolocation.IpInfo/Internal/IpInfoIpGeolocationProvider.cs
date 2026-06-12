using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Granit.IpGeolocation.IpInfo.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.IpInfo.Internal;

/// <summary>
/// Opt-in <see cref="IIpGeolocationProvider"/> backed by the ipinfo.io HTTP API.
/// </summary>
/// <remarks>
/// The IP is sent to the external API in the request path and the token as a <c>Bearer</c> header. Failures are
/// caught here and the request URI (which contains the raw IP) is never logged — only a failure category is.
/// Note that outbound HTTP instrumentation may still record the URL, so prefer the offline MaxMind provider for
/// strict data-minimisation.
/// </remarks>
internal sealed partial class IpInfoIpGeolocationProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<IpInfoIpGeolocationOptions> options,
    ILogger<IpInfoIpGeolocationProvider> logger) : IIpGeolocationProvider
{
    internal const string HttpClientName = "Granit.IpGeolocation.IpInfo";

    public string ProviderName => options.Value.ProviderName;

    public async Task<GeoLocation?> ResolveAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        // Defence-in-depth: the IP is interpolated into the request path, so reject anything that is not a bare
        // IP literal before it can override the validated base address (e.g. "//host", a scheme, or extra path).
        if (!IPAddress.TryParse(ipAddress, out IPAddress? parsed))
        {
            return null;
        }

        HttpClient client = httpClientFactory.CreateClient(HttpClientName);

        using HttpRequestMessage request = new(HttpMethod.Get, $"{parsed}/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string? token = options.Value.ApiToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRequestUnsuccessful((int)response.StatusCode);
                return null;
            }

            IpInfoResponse? body = await response.Content
                .ReadFromJsonAsync<IpInfoResponse>(cancellationToken)
                .ConfigureAwait(false);
            return Map(body);
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

    private static GeoLocation? Map(IpInfoResponse? body)
    {
        if (body is null || body.Bogon == true)
        {
            return null;
        }

        (double? latitude, double? longitude) = ParseLocation(body.Loc);
        if (body.City is null && body.Region is null && body.Country is null && latitude is null)
        {
            return null;
        }

        return new GeoLocation
        {
            City = body.City,
            Region = body.Region,
            CountryCode = body.Country,
            Latitude = latitude,
            Longitude = longitude,
        };
    }

    private static (double? Latitude, double? Longitude) ParseLocation(string? loc)
    {
        if (string.IsNullOrWhiteSpace(loc))
        {
            return (null, null);
        }

        string[] parts = loc.Split(',', 2);
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
        {
            return (latitude, longitude);
        }

        return (null, null);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "ipinfo.io geolocation request returned status {StatusCode}.")]
    private partial void LogRequestUnsuccessful(int statusCode);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "ipinfo.io geolocation lookup failed ({Reason}).")]
    private partial void LogLookupFailed(string reason);
}
