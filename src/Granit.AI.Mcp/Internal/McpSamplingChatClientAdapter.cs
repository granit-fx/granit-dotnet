using Granit.AI.Mcp.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Granit.AI.Mcp.Internal;

/// <summary>
/// Bridges MCP sampling requests (<c>CreateMessage</c>) to the Granit AI workspace
/// via <see cref="IAIChatClientFactory"/>. Validates every request through
/// <see cref="SamplingGuard"/> for cost control, rate limiting, and audit.
/// </summary>
/// <remarks>
/// Registered as the <see cref="McpServerOptions.SamplingHandler"/> callback.
/// When an external MCP server requests an LLM completion, this adapter:
/// <list type="number">
///   <item>Validates the request via <see cref="SamplingGuard"/></item>
///   <item>Converts MCP <see cref="SamplingMessage"/> to <see cref="ChatMessage"/></item>
///   <item>Forwards to the configured AI workspace</item>
///   <item>Converts the response back to <see cref="CreateMessageResult"/></item>
/// </list>
/// </remarks>
internal sealed partial class McpSamplingChatClientAdapter(
    IAIChatClientFactory chatClientFactory,
    SamplingGuard samplingGuard,
    IOptions<GranitAIMcpOptions> options,
    ILogger<McpSamplingChatClientAdapter> logger)
{
    /// <summary>
    /// Handles an MCP <c>CreateMessage</c> (sampling) request.
    /// </summary>
    public async Task<CreateMessageResult> HandleSamplingAsync(
        CreateMessageRequestParams request,
        CancellationToken cancellationToken)
    {
        string? serverOrigin = null;
        if (request.Metadata?.TryGetPropertyValue("serverOrigin", out System.Text.Json.Nodes.JsonNode? originNode) == true)
        {
            serverOrigin = originNode?.ToString();
        }

        int? maxTokens = request.MaxTokens > 0 ? request.MaxTokens : null;

        if (!samplingGuard.TryValidate(maxTokens, serverOrigin, out string? rejectionReason))
        {
            LogSamplingRejected(serverOrigin, rejectionReason!);
            return CreateRejectionResult(rejectionReason!);
        }

        IChatClient chatClient = await chatClientFactory
            .CreateAsync(options.Value.SamplingWorkspace, cancellationToken)
            .ConfigureAwait(false);

        List<ChatMessage> messages = ConvertMessages(request.Messages);
        ChatOptions chatOptions = BuildChatOptions(request);

        ChatResponse response = await chatClient
            .GetResponseAsync(messages, chatOptions, cancellationToken)
            .ConfigureAwait(false);

        LogSamplingCompleted(serverOrigin, (int?)response.Usage?.OutputTokenCount);

        return ConvertResponse(response);
    }

    private static List<ChatMessage> ConvertMessages(IList<SamplingMessage> mcpMessages)
    {
        List<ChatMessage> messages = new(mcpMessages.Count);
        foreach (SamplingMessage mcpMessage in mcpMessages)
        {
            ChatRole role = mcpMessage.Role == Role.Assistant
                ? ChatRole.Assistant
                : ChatRole.User;

            string text = ExtractText(mcpMessage.Content);
            messages.Add(new ChatMessage(role, text));
        }

        return messages;
    }

    private static string ExtractText(IList<ContentBlock> content)
    {
        foreach (ContentBlock block in content)
        {
            if (block is TextContentBlock textBlock)
            {
                return textBlock.Text;
            }
        }

        return string.Empty;
    }

    private static ChatOptions BuildChatOptions(CreateMessageRequestParams request)
    {
        ChatOptions chatOptions = new();

        if (request.MaxTokens > 0)
        {
            chatOptions.MaxOutputTokens = request.MaxTokens;
        }

        if (request.Temperature.HasValue)
        {
            chatOptions.Temperature = (float)request.Temperature.Value;
        }

        if (request.ModelPreferences?.Hints is { Count: > 0 } hints)
        {
            chatOptions.ModelId = hints[0].Name;
        }

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            chatOptions.Instructions = request.SystemPrompt;
        }

        return chatOptions;
    }

    private static CreateMessageResult ConvertResponse(ChatResponse response)
    {
        string content = string.Empty;
        if (response.Messages.Count > 0)
        {
            ChatMessage lastMessage = response.Messages[^1];
            content = lastMessage.Text ?? string.Empty;
        }

        return new CreateMessageResult
        {
            Content = [new TextContentBlock { Text = content }],
            Model = response.ModelId ?? "unknown",
            Role = Role.Assistant,
        };
    }

    private static CreateMessageResult CreateRejectionResult(string reason) =>
        new()
        {
            Content = [new TextContentBlock { Text = $"Sampling request rejected: {reason}" }],
            Model = "none",
            Role = Role.Assistant,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "MCP sampling request rejected from {ServerOrigin}: {Reason}")]
    private partial void LogSamplingRejected(string? serverOrigin, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "MCP sampling completed for {ServerOrigin} (outputTokens={OutputTokens})")]
    private partial void LogSamplingCompleted(string? serverOrigin, int? outputTokens);
}
