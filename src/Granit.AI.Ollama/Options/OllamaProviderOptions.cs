namespace Granit.AI.Ollama.Options;

/// <summary>
/// Configuration for the Ollama AI provider.
/// </summary>
/// <remarks>
/// <para>
/// Ollama runs AI models locally — no API key is required. By default, the provider
/// connects to <c>http://localhost:11434</c> and uses the <c>llama3.1</c> model.
/// </para>
/// <para>
/// Override <see cref="Endpoint"/> to point to a remote Ollama instance (e.g. on a
/// dedicated GPU server). Override <see cref="DefaultModel"/> to change the fallback
/// model when an <see cref="Granit.AI.Workspaces.AIWorkspace"/> does not specify one.
/// </para>
/// <para>
/// Endpoint changes are hot — <see cref="Internal.OllamaProviderFactory"/> picks up
/// the new endpoint whenever <c>IOptionsMonitor</c> publishes a change.
/// </para>
/// </remarks>
public sealed class OllamaProviderOptions
{
    /// <summary>
    /// Configuration section name used for <c>IConfiguration</c> binding.
    /// </summary>
    public const string SectionName = "AI:Ollama";

    /// <summary>
    /// The Ollama server endpoint URL. Defaults to <c>http://localhost:11434</c>.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:11434";

    /// <summary>
    /// The default model to use when an <see cref="Granit.AI.Workspaces.AIWorkspace"/> does not specify a model.
    /// Defaults to <c>llama3.1</c>.
    /// </summary>
    public string DefaultModel { get; set; } = "llama3.1";

    /// <summary>
    /// Optional allowlist of model identifiers callers are permitted to request. When empty
    /// (the default), any model installed on the Ollama server is allowed.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth against unauthorised model usage on shared GPU servers.
    /// <see cref="DefaultModel"/> must itself appear in this list when it is non-empty.
    /// </remarks>
    public IList<string> AllowedModels { get; set; } = [];

    /// <summary>
    /// Maximum duration of a single Ollama HTTP call. Defaults to 5 minutes since
    /// local-inference latency dominates for larger models (no network).
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
