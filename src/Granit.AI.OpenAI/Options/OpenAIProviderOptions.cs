namespace Granit.AI.OpenAI.Options;

/// <summary>
/// Configuration options for the OpenAI AI provider.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:OpenAI</c> configuration section.
/// The <see cref="ApiKey"/> should be injected from <c>Granit.Vault</c>; never hardcode it.
/// Rotation is hot — <see cref="Internal.OpenAIProviderFactory"/> rebuilds its underlying
/// SDK client whenever <c>IOptionsMonitor</c> publishes a change.
/// </remarks>
public sealed class OpenAIProviderOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:OpenAI";

    /// <summary>
    /// OpenAI API key. Required. Inject from <c>Granit.Vault</c>; never hardcode.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional custom endpoint URL for OpenAI-compatible APIs (e.g. Azure OpenAI, local proxies).
    /// </summary>
    /// <remarks>
    /// When <c>null</c> or empty, the default OpenAI endpoint (<c>https://api.openai.com/v1</c>) is used.
    /// </remarks>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Default chat model to use when the workspace does not specify one.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>
    /// Default embedding model to use for embedding generation.
    /// </summary>
    public string DefaultEmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Optional allowlist of model identifiers callers are permitted to request.
    /// When empty (the default), any model identifier accepted by the OpenAI API is allowed.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth against cost amplification: a tenant-controlled workspace cannot direct
    /// traffic to a more expensive model tier than the operator approved. <see cref="DefaultModel"/>
    /// and <see cref="DefaultEmbeddingModel"/> must themselves appear in this list when it is non-empty.
    /// </remarks>
    public IList<string> AllowedModels { get; set; } = [];

    /// <summary>
    /// Maximum duration of a single OpenAI HTTP call (including DNS, connect, request, response).
    /// Retries are governed by <see cref="MaxRetries"/> and are not counted in this budget.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of retries the SDK performs on transient failures (5xx, throttling) with exponential
    /// backoff. Set to <c>0</c> to disable retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
