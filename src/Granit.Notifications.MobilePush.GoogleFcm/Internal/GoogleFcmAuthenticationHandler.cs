using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.MobilePush.GoogleFcm.Internal;

/// <summary>
/// Attaches the OAuth 2.0 Bearer token to every FCM HTTP v1 request.
/// On 401 the cached token is invalidated and the request retried once with a fresh token.
/// </summary>
internal sealed partial class GoogleFcmAuthenticationHandler(
    GoogleFcmTokenProvider tokenProvider,
    ILogger<GoogleFcmAuthenticationHandler> logger) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is not HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Token may have been revoked server-side before its local expiry — mint a fresh one and retry once.
        LogUnauthorizedRetry(request.RequestUri?.AbsolutePath ?? "unknown");
        tokenProvider.Invalidate();
        accessToken = await tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        response.Dispose();

        using HttpRequestMessage retryRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await base.SendAsync(retryRequest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(
        HttpRequestMessage original,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers
            .Where(h => !string.Equals(h.Key, "Authorization", StringComparison.OrdinalIgnoreCase)))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is not null)
        {
            byte[] contentBytes = await original.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            clone.Content = new ByteArrayContent(contentBytes);

            if (original.Content.Headers.ContentType is not null)
            {
                clone.Content.Headers.ContentType = original.Content.Headers.ContentType;
            }
        }

        return clone;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "FCM returned 401 for {RequestPath}; refreshing the access token and retrying once")]
    private partial void LogUnauthorizedRetry(string requestPath);
}
