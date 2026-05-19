namespace Granit.AI.Anthropic.Options;

/// <summary>
/// Configuration for the Anthropic (Claude) AI provider.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Anthropic</c> configuration section.
/// The <see cref="ApiKey"/> should be injected from <c>Granit.Vault</c>; never hardcode it.
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
    /// Supported models include <c>claude-opus-4-6</c>, <c>claude-sonnet-4-6</c>,
    /// and <c>claude-haiku-4-5</c>.
    /// </remarks>
    public string DefaultModel { get; set; } = "claude-sonnet-4-6";
}
