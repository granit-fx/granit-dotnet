using Granit.AI;
using Granit.AI.Workspaces;
using Granit.Imaging.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Default <see cref="IImageTextExtractor"/>. Resolves a Vision-capable workspace (an explicitly
/// configured one is trusted; otherwise the first workspace whose model reports
/// <see cref="AIModelCapabilities.Vision"/>), sends the image as a <see cref="DataContent"/> with a
/// bounded extraction instruction, and stamps a dedicated usage record for the vision call.
/// </summary>
internal sealed partial class LlmImageTextExtractor(
    IAIChatClientFactory chatClientFactory,
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver,
    IOptions<ImagingAIOptions> options,
    ILogger<LlmImageTextExtractor> logger) : IImageTextExtractor
{
    private const string ExtractionPrompt =
        "Extract and return all text visible in this image, verbatim and in reading order. "
        + "Return only the extracted text with no commentary. If the image contains no text, "
        + "return an empty response.";

    public async Task<ImageTextExtractionResult?> ExtractTextAsync(
        ReadOnlyMemory<byte> imageData,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        AIWorkspace? workspace = await ResolveVisionWorkspaceAsync(cancellationToken).ConfigureAwait(false);
        if (workspace is null)
        {
            LogNoVisionWorkspace();
            return null;
        }

        ImagingAIOptions opts = options.Value;

        using IChatClient client = await chatClientFactory
            .CreateAsync(workspace.Key, cancellationToken)
            .ConfigureAwait(false);

        ChatMessage message = new(ChatRole.User,
        [
            new TextContent(ExtractionPrompt),
            new DataContent(imageData, contentType),
        ]);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

        // Usage is stamped by the factory-applied middleware (per ADR-067 the vision call
        // records its own usage, now via the IChatClient pipeline).
        ChatResponse response = await client
            .GetResponseAsync([message], cancellationToken: timeoutCts.Token)
            .ConfigureAwait(false);

        return new ImageTextExtractionResult(response.Text ?? string.Empty, workspace.Key);
    }

    private async Task<AIWorkspace?> ResolveVisionWorkspaceAsync(CancellationToken cancellationToken)
    {
        string? configured = options.Value.WorkspaceName;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            // Explicitly configured workspace is trusted — the admin opted into it for vision.
            return await workspaceProvider.GetAsync(configured, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<AIWorkspace> all = await workspaceProvider.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (AIWorkspace candidate in all)
        {
            AIModelCapabilities? capabilities = await capabilityResolver
                .ResolveAsync(candidate.Provider, candidate.Model, cancellationToken)
                .ConfigureAwait(false);

            if (capabilities?.Vision == true)
            {
                return candidate;
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "No vision-capable AI workspace is configured; extract_text_from_image is unavailable.")]
    private partial void LogNoVisionWorkspace();
}
