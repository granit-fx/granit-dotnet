namespace Granit.AI.AzureOpenAI.Options;

/// <summary>
/// Configuration for the Azure OpenAI provider.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:AzureOpenAI</c> configuration section.
/// When <see cref="ApiKey"/> is empty, the provider falls back to
/// <c>DefaultAzureCredential</c> (Managed Identity) — the recommended
/// approach for production deployments.
/// Rotation is hot — <see cref="Internal.AzureOpenAIProviderFactory"/> rebuilds its underlying
/// SDK client whenever <c>IOptionsMonitor</c> publishes a change.
/// </remarks>
public sealed class AzureOpenAIProviderOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AI:AzureOpenAI";

    /// <summary>
    /// Azure OpenAI resource endpoint (e.g. <c>https://my-resource.openai.azure.com</c>).
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication. When empty, <c>DefaultAzureCredential</c> is used instead.
    /// </summary>
    /// <remarks>
    /// Inject from <c>Granit.Vault</c>; never hardcode. Leave empty in production
    /// to use Managed Identity (zero secrets).
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Default deployment name (e.g. <c>gpt-4o</c>). Used when a workspace does not specify a model.
    /// </summary>
    public string DefaultDeployment { get; set; } = "gpt-4o";

    /// <summary>
    /// Default embedding deployment name.
    /// </summary>
    public string DefaultEmbeddingDeployment { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Optional allowlist of deployment names callers are permitted to request. When empty
    /// (the default), any deployment configured on the Azure OpenAI resource is allowed.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth against cost amplification: a tenant-controlled workspace cannot direct
    /// traffic to a more expensive deployment than the operator approved.
    /// <see cref="DefaultDeployment"/> and <see cref="DefaultEmbeddingDeployment"/> must
    /// themselves appear in this list when it is non-empty.
    /// </remarks>
    public IList<string> AllowedDeployments { get; set; } = [];

    /// <summary>
    /// Maximum duration of a single Azure OpenAI HTTP call. Retries are governed by
    /// <see cref="MaxRetries"/> and are not counted in this budget.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Number of retries the SDK performs on transient failures (5xx, throttling) with exponential
    /// backoff. Set to <c>0</c> to disable retries.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// When <c>true</c>, the resolver falls back to <c>DefaultAzureCredential</c> (Managed Identity)
    /// when no ApiKey is configured at any cascade layer. Defaults to <c>false</c> (audit VULN-103):
    /// silently switching from API-key to Managed Identity changes the trust principal in a way
    /// that may have unintended privilege implications.
    /// </summary>
    public bool AllowManagedIdentityFallback { get; set; }
}
