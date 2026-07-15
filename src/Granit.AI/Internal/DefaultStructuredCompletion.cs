using System.Diagnostics;
using System.Text.Json;
using Granit.AI.Diagnostics;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Internal;

/// <summary>
/// Default <see cref="IStructuredCompletion"/>: resolves the workspace, applies the quota
/// guard, pins the output with a provider-enforced JSON schema (falling back to schema-in-prompt
/// + fence-strip when the model lacks <see cref="AIModelCapabilities.StructuredOutput"/>),
/// deserializes, and maps every failure to a PII-safe result. Usage is stamped by the
/// factory-applied middleware.
/// </summary>
internal sealed partial class DefaultStructuredCompletion(
    IAIChatClientFactory chatClientFactory,
    IAIWorkspaceProvider workspaceProvider,
    IAIWorkspaceCapabilityResolver capabilityResolver,
    IAIQuotaGuard quotaGuard,
    AIMetrics metrics,
    IOptions<StructuredCompletionOptions> options,
    IOptions<GranitAIOptions> aiOptions,
    ILogger<DefaultStructuredCompletion> logger) : IStructuredCompletion
{
    private const string DefaultInstruction = "Produce structured data from the following content.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public async Task<StructuredCompletionResult<T>> CompleteAsync<T>(
        StructuredCompletionRequest request,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content) && request.Attachments is not { Count: > 0 })
        {
            throw new ArgumentException(
                "The request must carry Content, at least one attachment, or both.", nameof(request));
        }

        string workspaceName = request.WorkspaceName ?? aiOptions.Value.DefaultWorkspace;

        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            LogWorkspaceNotFound(workspaceName);
            return new StructuredCompletionResult<T>
            {
                Status = StructuredCompletionStatus.TransportFailure,
                ErrorMessage = $"AI workspace '{workspaceName}' is not registered.",
            };
        }

        string? tenantId = workspace.TenantId?.ToString();

        AIQuotaResult quota = await quotaGuard.CheckAsync(cancellationToken).ConfigureAwait(false);
        if (!quota.IsAllowed)
        {
            LogQuotaDenied(typeof(T).Name);
            metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "quota_denied");
            return new StructuredCompletionResult<T>
            {
                Status = StructuredCompletionStatus.TransportFailure,
                ModelId = workspace.Model,
                ErrorMessage = "AI quota exceeded for the current tenant.",
            };
        }

        StructuredCompletionOptions opts = options.Value;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using Activity? activity = AIActivitySource.Instance.StartActivity("ai.structured_completion");
        activity?.SetTag("ai.workspace", workspaceName);
        activity?.SetTag("ai.provider", workspace.Provider);
        activity?.SetTag("ai.model", workspace.Model);
        activity?.SetTag("ai.result_type", typeof(T).Name);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            AIModelCapabilities? capabilities = await capabilityResolver
                .ResolveAsync(workspace.Provider, workspace.Model, linkedCts.Token)
                .ConfigureAwait(false);
            bool schemaSupported = capabilities?.StructuredOutput ?? false;

            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
                .CreateAsync(workspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            // Text prompt first (instruction + sanitized envelope), then the binary parts —
            // the pattern vision-capable providers expect for multimodal user messages.
            List<AIContent> parts = [new TextContent(BuildPrompt<T>(request, schemaSupported))];
            if (request.Attachments is { Count: > 0 })
            {
                parts.AddRange(request.Attachments);
            }

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, parts),
            };

            ChatOptions? chatOptions = schemaSupported
                ? new ChatOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<T>() }
                : null;

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, chatOptions, linkedCts.Token)
                .ConfigureAwait(false);

            stopwatch.Stop();

            string? modelId = response.ModelId ?? workspace.Model;
            ChatFinishReason? finishReason = response.FinishReason;
            IReadOnlyDictionary<string, object?>? responseMetadata = response.AdditionalProperties;
            UsageDetails? usage = response.Usage;

            metrics.RecordRequestDuration(tenantId, workspace.Model, workspace.Provider, stopwatch.Elapsed);

            string text = response.Text ?? string.Empty;

            if (finishReason == ChatFinishReason.ContentFilter || string.IsNullOrWhiteSpace(text))
            {
                LogModelRefused(typeof(T).Name);
                metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "model_refused");
                return Build<T>(StructuredCompletionStatus.ModelRefused, null, modelId,
                    "The model returned no usable content.", finishReason, responseMetadata, usage);
            }

            if (!schemaSupported)
            {
                text = LlmResponseHelper.StripMarkdownCodeFences(text);
            }

            T? value;
            try
            {
                value = JsonSerializer.Deserialize<T>(text, SerializerOptions);
            }
            catch (JsonException ex)
            {
                // Never surface ex.Message: a JSON parse error embeds a fragment of the
                // (LLM-produced, possibly PII-bearing) response. Type only.
                LogSchemaViolation(typeof(T).Name, ex.GetType().Name);
                metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "schema_violation");
                return Build<T>(StructuredCompletionStatus.SchemaViolation, null, modelId,
                    "The model response did not match the expected schema.", finishReason, responseMetadata, usage);
            }

            if (value is null)
            {
                LogSchemaViolation(typeof(T).Name, "NullResult");
                metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "schema_violation");
                return Build<T>(StructuredCompletionStatus.SchemaViolation, null, modelId,
                    "The model response deserialized to null.", finishReason, responseMetadata, usage);
            }

            LogSucceeded(typeof(T).Name);
            metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "succeeded");
            return Build(StructuredCompletionStatus.Succeeded, value, modelId, null, finishReason, responseMetadata, usage);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTimeout(typeof(T).Name, opts.TimeoutSeconds);
            metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "timeout");
            return new StructuredCompletionResult<T>
            {
                Status = StructuredCompletionStatus.TransportFailure,
                ModelId = workspace.Model,
                ErrorMessage = $"The AI request timed out after {opts.TimeoutSeconds} seconds.",
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never surface ex.Message: some IChatClient providers echo the prompt payload
            // in transport exception messages (4xx content-policy / schema reject).
            LogTransportFailed(typeof(T).Name, ex.GetType().Name);
            metrics.RecordRequestCompleted(tenantId, workspace.Model, workspace.Provider, "transport_failure");
            return new StructuredCompletionResult<T>
            {
                Status = StructuredCompletionStatus.TransportFailure,
                ModelId = workspace.Model,
                ErrorMessage = "The AI request failed due to a transport or provider error.",
            };
        }
    }

    private static string BuildPrompt<T>(StructuredCompletionRequest request, bool schemaSupported)
    {
        var builder = new PromptBuilder();

        builder.AppendInstruction(string.IsNullOrWhiteSpace(request.Instruction)
            ? DefaultInstruction
            : request.Instruction);

        if (!schemaSupported)
        {
            // Providers without native structured output get the schema in-prompt; the
            // response is fence-stripped and deserialized on the way back.
            builder.AppendInstruction(
                "Respond with a single JSON object matching this schema, with no prose or Markdown:");
            builder.AppendInstruction(AIJsonUtilities.CreateJsonSchema(typeof(T)).ToString());
        }

        if (request.Context is { Count: > 0 })
        {
            builder.AppendUserDataMap("Context", request.Context);
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            builder.AppendUserTextBlock(request.ContentLabel ?? "Document", request.Content);
        }

        return builder.Build();
    }

    private static StructuredCompletionResult<T> Build<T>(
        StructuredCompletionStatus status,
        T? value,
        string? modelId,
        string? errorMessage,
        ChatFinishReason? finishReason,
        IReadOnlyDictionary<string, object?>? metadata,
        UsageDetails? usage)
        where T : class =>
        new()
        {
            Status = status,
            Value = value,
            ModelId = modelId,
            ErrorMessage = errorMessage,
            FinishReason = finishReason,
            Metadata = metadata,
            Usage = usage,
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Structured completion: workspace {WorkspaceName} not found")]
    private partial void LogWorkspaceNotFound(string workspaceName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Structured completion for {TypeName} denied by quota guard")]
    private partial void LogQuotaDenied(string typeName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Structured completion succeeded for {TypeName}")]
    private partial void LogSucceeded(string typeName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Structured completion for {TypeName} refused by model (empty or content-filtered)")]
    private partial void LogModelRefused(string typeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Structured completion schema violation for {TypeName} (reason: {Reason})")]
    private partial void LogSchemaViolation(string typeName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Structured completion timed out for {TypeName} after {TimeoutSeconds}s")]
    private partial void LogTimeout(string typeName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Structured completion failed for {TypeName} (exception type: {ExceptionType})")]
    private partial void LogTransportFailed(string typeName, string exceptionType);
}
