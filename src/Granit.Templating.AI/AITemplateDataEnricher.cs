using Granit.AI;
using Granit.Templating.AI.Options;
using Granit.Templating.Enrichment;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Templating.AI;

/// <summary>
/// Base class for AI-powered template data enrichers that use an LLM to produce
/// computed values (summaries, translations, recommendations) for template rendering.
/// </summary>
/// <typeparam name="TData">The data model type. Must be non-null.</typeparam>
/// <remarks>
/// Subclasses provide the prompt and specify which property to enrich via
/// <see cref="BuildPrompt"/> and <see cref="ApplyEnrichment"/>. The base class handles
/// <c>IChatClient</c> resolution, timeout, and graceful degradation (returns the original
/// data unchanged on failure).
/// <para>
/// <strong>Security (GDPR/LLM06):</strong> <see cref="BuildPrompt"/> receives the full
/// <typeparamref name="TData"/> business object. Subclass implementations MUST NOT include
/// PII (personal names, email, addresses, financial data) in the prompt unless the LLM provider
/// has a Data Processing Agreement (DPA) that covers the tenant's jurisdiction. Use
/// <c>PromptBuilder</c> from <c>Granit.AI</c> to sanitize user-controlled input.
/// </para>
/// </remarks>
public abstract partial class AITemplateDataEnricher<TData>(
    IAIChatClientFactory chatClientFactory,
    IOptions<TemplatingAIOptions> options,
    ILogger logger) : ITemplateDataEnricher<TData>
    where TData : notnull
{
    /// <inheritdoc/>
    public abstract int Order { get; }

    /// <summary>
    /// Builds the prompt to send to the LLM for enriching the given data.
    /// </summary>
    /// <param name="data">The current data model instance.</param>
    /// <returns>A prompt string for the LLM.</returns>
    /// <remarks>
    /// <strong>Security:</strong> Use <c>PromptBuilder</c> from <c>Granit.AI</c> to sanitize
    /// any user-controlled fields included in the prompt. Do not interpolate raw user input
    /// directly (prompt injection risk — OWASP LLM01). Avoid including PII unless the
    /// LLM provider has an appropriate DPA (OWASP LLM06).
    /// </remarks>
    protected abstract string BuildPrompt(TData data);

    /// <summary>
    /// Applies the LLM response to the data model, returning a new enriched instance.
    /// </summary>
    /// <param name="data">The original data model. Do not mutate.</param>
    /// <param name="llmResponse">The text response from the LLM.</param>
    /// <returns>A new <typeparamref name="TData"/> with the enriched property set.</returns>
    protected abstract TData ApplyEnrichment(TData data, string llmResponse);

    /// <inheritdoc/>
    public async Task<TData> EnrichAsync(TData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        TemplatingAIOptions opts = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(data);

            ChatResponse response = await chatClient
                .GetResponseAsync(prompt, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return data;
            }

            return ApplyEnrichment(data, responseText);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogEnrichmentFailed(logger, ex);
            return data;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI template data enrichment failed, returning original data (graceful degradation)")]
    private static partial void LogEnrichmentFailed(ILogger logger, Exception exception);
}
