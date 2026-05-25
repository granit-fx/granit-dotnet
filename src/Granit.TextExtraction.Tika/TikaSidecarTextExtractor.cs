using System.Net.Http.Headers;
using Granit.TextExtraction.Options;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Tika;

/// <summary>
/// Extractor that forwards bytes to an Apache Tika sidecar over HTTP and reads back
/// plain text. The extractor itself owns no Tika parsing logic — it is a thin transport
/// layer with cap enforcement on both directions of the call.
/// </summary>
/// <remarks>
/// <para>
/// The named <see cref="HttpClient"/> <c>granit-tika</c> must be registered by the host
/// via <c>AddTikaSidecarExtractor(...)</c>. The host wires the primary message handler
/// (mTLS client cert, mutual auth, etc.) — this package does not assume a specific
/// cert provider so it can be used with Granit.Vault, files on disk, or any custom
/// integration without coupling.
/// </para>
/// <para>
/// VULN-102 enforcement: <see cref="TikaSidecarOptions.AllowedHosts"/> is checked at
/// startup (module validation) and the request body is wrapped in
/// <see cref="LimitedStream"/> with <see cref="GranitTextExtractionOptions.MaxBodySizeBytes"/>
/// before upload. Response is truncated at <c>maxCharLength</c> on read.
/// </para>
/// </remarks>
public sealed partial class TikaSidecarTextExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.tika";

    /// <summary>Named HTTP client key — host registration must use this name.</summary>
    public const string HttpClientName = "granit-tika";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GranitTextExtractionOptions _extractionOptions;
    private readonly TikaSidecarOptions _tikaOptions;
    private readonly ILogger<TikaSidecarTextExtractor> _logger;
    private readonly HashSet<string> _allowedContentTypes;

    public TikaSidecarTextExtractor(
        IHttpClientFactory httpClientFactory,
        IOptions<GranitTextExtractionOptions> extractionOptions,
        IOptions<TikaSidecarOptions> tikaOptions,
        ILogger<TikaSidecarTextExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(extractionOptions);
        ArgumentNullException.ThrowIfNull(tikaOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClientFactory = httpClientFactory;
        _extractionOptions = extractionOptions.Value;
        _tikaOptions = tikaOptions.Value;
        _logger = logger;
        _allowedContentTypes = new HashSet<string>(
            _tikaOptions.AllowedContentTypes,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public string Name => ExtractorName;

    /// <inheritdoc/>
    public bool CanHandle(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return _allowedContentTypes.Contains(contentType);
    }

    /// <inheritdoc/>
    public async Task<TextExtractionResult> ExtractAsync(
        Stream source,
        string contentType,
        int maxCharLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrEmpty(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharLength);

        // VULN-001: read the request body through a LimitedStream so a malicious
        // upstream can't push more than MaxBodySizeBytes at us before Tika even sees
        // the bytes. Materialised to byte[] because HttpContent expects a content-length
        // for the Tika endpoint to allocate efficiently.
        byte[] body = await ReadAllBytesAsync(
            source, _extractionOptions.MaxBodySizeBytes, cancellationToken).ConfigureAwait(false);

        using HttpClient client = _httpClientFactory.CreateClient(HttpClientName);
        client.Timeout = _tikaOptions.Timeout;

        Uri endpoint = new(_tikaOptions.Uri, "tika");
        using HttpRequestMessage request = new(HttpMethod.Put, endpoint);
        using ByteArrayContent content = new(body);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Content = content;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        // VULN-400: refuse the recursive parser path. Tika historically had SSRF / fetch
        // CVEs in embedded-resource handling (CVE-2022-30126 etc.); the option lets the
        // host opt back in only when it explicitly wants recursive indexing.
        if (_tikaOptions.SkipEmbeddedResources)
        {
            request.Headers.Add("X-Tika-Skip-Embedded-Resources", "true");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LogTikaTransportFailure(ex, endpoint);
            return Skipped();
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogTikaTimeout(ex, endpoint, _tikaOptions.Timeout);
            return Skipped();
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                LogTikaUnsuccessfulStatus((int)response.StatusCode, endpoint);
                return Skipped();
            }

            return await ReadCappedResponseAsync(
                response, maxCharLength, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(
        Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        LimitedStream limited = new(source, maxBytes);
        using MemoryStream buffer = new();
        await limited.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    private static async Task<TextExtractionResult> ReadCappedResponseAsync(
        HttpResponseMessage response, int maxCharLength, CancellationToken cancellationToken)
    {
        // Tika streams plain text; we cap at maxCharLength on read so a malicious
        // sidecar can't return an unbounded body.
        await using Stream responseStream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        using StreamReader reader = new(responseStream, leaveOpen: false);
        char[] buffer = new char[4096];
        System.Text.StringBuilder sb = new(Math.Min(maxCharLength, 8192));
        bool truncated = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) { break; }

            int remaining = maxCharLength - sb.Length;
            if (read >= remaining)
            {
                sb.Append(buffer, 0, Math.Max(remaining, 0));
                truncated = true;
                break;
            }

            sb.Append(buffer, 0, read);
        }

        string text = sb.ToString();
        return new TextExtractionResult(
            Content: text,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: text.Length,
            ExtractorName: ExtractorName,
            // Tika is a deterministic parser farm (not a model). Provenance stays Deterministic.
            Confidence: ExtractionConfidence.Deterministic);
    }

    private static TextExtractionResult Skipped() =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: ExtractorName,
            Confidence: ExtractionConfidence.Deterministic);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TikaSidecarTextExtractor transport failure calling {Endpoint}.")]
    private partial void LogTikaTransportFailure(Exception exception, Uri endpoint);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TikaSidecarTextExtractor timed out after {Timeout} calling {Endpoint}.")]
    private partial void LogTikaTimeout(Exception exception, Uri endpoint, TimeSpan timeout);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "TikaSidecarTextExtractor received unsuccessful status {StatusCode} from {Endpoint}.")]
    private partial void LogTikaUnsuccessfulStatus(int statusCode, Uri endpoint);
}
