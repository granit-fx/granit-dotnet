namespace Granit.AI.Anthropic.Options;

/// <summary>
/// Configuration for the Anthropic (Claude) AI provider.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Anthropic</c> configuration section.
/// The <see cref="ApiKey"/> should be injected from <c>Granit.Vault</c>; never hardcode it.
/// Rotation is hot — <see cref="Internal.AnthropicProviderFactory"/> rebuilds its underlying
/// SDK client whenever <c>IOptionsMonitor</c> publishes a change.
/// </remarks>
public sealed class AnthropicProviderOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AI:Anthropic";

    /// <summary>
    /// Anthropic API key. Required. Inject from <c>Granit.Vault</c>; never hardcode.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Default chat model to use when the workspace does not specify one.
    /// </summary>
    /// <remarks>
    /// Use any current Claude model identifier (Opus, Sonnet, or Haiku) — refer to
    /// Anthropic's official model list rather than relying on examples in this docstring.
    /// </remarks>
    public string DefaultModel { get; set; } = "claude-sonnet-4-6";

    /// <summary>
    /// Optional allowlist of Claude model identifiers callers are permitted to request.
    /// When empty (the default), any model identifier accepted by the Anthropic API is allowed.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth against cost amplification: a tenant-controlled workspace cannot direct
    /// traffic to a more expensive model tier than the operator approved. <see cref="DefaultModel"/>
    /// must itself appear in this list when it is non-empty.
    /// </remarks>
    public IList<string> AllowedModels { get; set; } = [];

    /// <summary>
    /// Maximum duration of a single Anthropic HTTP call (including DNS, connect, request, response).
    /// Retries are governed by <see cref="MaxRetries"/> and are not counted in this budget.
    /// </summary>
    /// <remarks>
    /// Two minutes is a balance between extended-thinking model latency and bounded thread-pool
    /// pressure. The SDK default (10 minutes) is too long for production traffic.
    /// </remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of retries the SDK performs on transient failures (connection errors, 408, 429, 5xx)
    /// with exponential backoff. Set to <c>0</c> to disable retries.
    /// </summary>
    public int MaxRetries { get; set; } = 2;
}
