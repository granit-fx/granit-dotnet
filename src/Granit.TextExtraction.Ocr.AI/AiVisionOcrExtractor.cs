using Granit.AI;
using Granit.TextExtraction.Ocr.AI.Options;
using Granit.TextExtraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Ocr.AI;

/// <summary>
/// Extractor that sends raster images to a multimodal LLM (vision-capable
/// <see cref="IChatClient"/>) and reads back the verbatim text. Backed by
/// <see cref="IAIChatClientFactory"/>; the model + endpoint are resolved from the
/// Granit.AI workspace named by <see cref="AiVisionOcrOptions.WorkspaceName"/>.
/// </summary>
/// <remarks>
/// <para>
/// Enabling this extractor sends document bytes to a third-party LLM provider. The host
/// MUST disclose this sub-processor in its GDPR Article 28 DPA chain. For on-prem-only
/// deployments, point the workspace at Ollama / vLLM / LM Studio.
/// </para>
/// <para>
/// Cost ceiling: inherited from <c>Granit.AI.Options.AIQuotaOptions.MaxRequestsPerTenantPerHour</c>
/// — this extractor does not implement its own counter; the IChatClient pipeline already
/// applies the quota middleware.
/// </para>
/// </remarks>
public sealed partial class AiVisionOcrExtractor : ITextExtractor
{
    /// <summary>The stable extractor identifier surfaced on metrics and spans.</summary>
    public const string ExtractorName = "granit.text-extraction.ocr-ai";

    private readonly IAIChatClientFactory _chatClientFactory;
    private readonly IVisionOcrPromptBuilder _promptBuilder;
    private readonly GranitTextExtractionOptions _extractionOptions;
    private readonly AiVisionOcrOptions _ocrOptions;
    private readonly ILogger<AiVisionOcrExtractor> _logger;
    private readonly HashSet<string> _allowedContentTypes;

    public AiVisionOcrExtractor(
        IAIChatClientFactory chatClientFactory,
        IVisionOcrPromptBuilder promptBuilder,
        IOptions<GranitTextExtractionOptions> extractionOptions,
        IOptions<AiVisionOcrOptions> ocrOptions,
        ILogger<AiVisionOcrExtractor> logger)
    {
        ArgumentNullException.ThrowIfNull(chatClientFactory);
        ArgumentNullException.ThrowIfNull(promptBuilder);
        ArgumentNullException.ThrowIfNull(extractionOptions);
        ArgumentNullException.ThrowIfNull(ocrOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _chatClientFactory = chatClientFactory;
        _promptBuilder = promptBuilder;
        _extractionOptions = extractionOptions.Value;
        _ocrOptions = ocrOptions.Value;
        _logger = logger;
        _allowedContentTypes = new HashSet<string>(
            _ocrOptions.AllowedContentTypes,
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

        // VULN-001: cap the request body before sending to the model. The byte cap is also
        // the effective pixel-bomb defence — a 100k×100k decoded image far exceeds 100 MB
        // long before reaching the provider.
        byte[] bytes = await ReadAllBytesAsync(
            source, _extractionOptions.MaxBodySizeBytes, cancellationToken).ConfigureAwait(false);

        IChatClient chatClient;
        try
        {
            chatClient = await _chatClientFactory
                .CreateAsync(_ocrOptions.WorkspaceName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogChatClientResolutionFailed(ex, _ocrOptions.WorkspaceName ?? "<default>");
            return Skipped();
        }

        try
        {
            using IChatClient _ = chatClient;

            ChatMessage userMessage = new(ChatRole.User,
            [
                new TextContent(_promptBuilder.BuildPrompt(contentType, maxCharLength)),
                new DataContent(bytes, contentType),
            ]);

            ChatResponse response = await chatClient
                .GetResponseAsync([userMessage], cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            string text = response.Text ?? string.Empty;
            return Truncate(text, maxCharLength);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogChatClientCallFailed(ex, _ocrOptions.WorkspaceName ?? "<default>");
            return Skipped();
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

    private static TextExtractionResult Truncate(string content, int maxCharLength)
    {
        bool truncated = content.Length > maxCharLength;
        string output = truncated ? content[..maxCharLength] : content;

        return new TextExtractionResult(
            Content: output,
            DetectedLanguage: null,
            IsTruncated: truncated,
            CharCount: output.Length,
            ExtractorName: ExtractorName);
    }

    private static TextExtractionResult Skipped() =>
        new(Content: string.Empty,
            DetectedLanguage: null,
            IsTruncated: true,
            CharCount: 0,
            ExtractorName: ExtractorName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AiVisionOcrExtractor failed to resolve IChatClient for workspace {Workspace}.")]
    private partial void LogChatClientResolutionFailed(Exception exception, string workspace);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AiVisionOcrExtractor IChatClient call failed for workspace {Workspace}.")]
    private partial void LogChatClientCallFailed(Exception exception, string workspace);
}
