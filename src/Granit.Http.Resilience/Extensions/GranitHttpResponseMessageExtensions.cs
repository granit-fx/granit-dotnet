using Granit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Http.Resilience.Extensions;

/// <summary>
/// Shared success-guard for HTTP-based provider senders (SendGrid, Scaleway, Brevo,
/// Twilio, Zulip…): reads the error body best-effort, logs it <b>scrubbed</b> (vendor
/// error payloads routinely echo the recipient or message content back), and throws an
/// <see cref="HttpRequestException"/> carrying the status code but never the body —
/// exception messages end up in delivery audit rows and upstream logs.
/// </summary>
public static partial class GranitHttpResponseMessageExtensions
{
    /// <summary>
    /// Throws an <see cref="HttpRequestException"/> when <paramref name="response"/> is not
    /// a success status, after logging the scrubbed error body at Warning.
    /// </summary>
    /// <param name="response">The provider HTTP response to check.</param>
    /// <param name="logger">Logger of the calling sender.</param>
    /// <param name="providerName">Human-readable provider name for the log/exception ("SendGrid").</param>
    /// <param name="endpoint">Optional endpoint label to disambiguate multi-endpoint providers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task EnsureGranitSuccessAsync(
        this HttpResponseMessage response,
        ILogger logger,
        string providerName,
        string? endpoint = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? errorBody = null;
        try
        {
            errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort — never mask the original HTTP error with a body-read failure.
        }

        LogProviderApiError(
            logger,
            providerName,
            endpoint ?? "(default)",
            (int)response.StatusCode,
            errorBody is null ? "(no body)" : LogRedaction.Scrub(errorBody));

        throw new HttpRequestException(
            endpoint is null
                ? $"{providerName} API error {(int)response.StatusCode}"
                : $"{providerName} API error {(int)response.StatusCode} on {endpoint}",
            inner: null,
            response.StatusCode);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ProviderName} API error on {Endpoint}: HTTP {StatusCode} — {ScrubbedErrorBody}")]
    private static partial void LogProviderApiError(
        ILogger logger, string providerName, string endpoint, int statusCode, string scrubbedErrorBody);
}
