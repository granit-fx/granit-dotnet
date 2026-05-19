namespace Granit.AI.Tenancy;

/// <summary>
/// Canonical setting names used by the AI credential cascade.
/// Backed by <see cref="AISettingDefinitionProvider"/>; consumers should reference
/// constants rather than literal strings.
/// </summary>
public static class AISettingNames
{
    private const string Prefix = "Granit.AI.";

    /// <summary>Prefix used to discriminate AI-related settings (for write authorisation).</summary>
    public const string PrefixValue = Prefix;

    /// <summary>Anthropic provider settings.</summary>
    public static class Anthropic
    {
        /// <summary>Anthropic API key — encrypted, T+G scope.</summary>
        public const string ApiKey = Prefix + "Anthropic.ApiKey";
    }

    /// <summary>OpenAI provider settings.</summary>
    public static class OpenAI
    {
        /// <summary>OpenAI API key — encrypted, T+G scope.</summary>
        public const string ApiKey = Prefix + "OpenAI.ApiKey";

        /// <summary>OpenAI-compatible endpoint override — T+G scope (URL, not encrypted).</summary>
        public const string Endpoint = Prefix + "OpenAI.Endpoint";
    }

    /// <summary>Azure OpenAI provider settings.</summary>
    public static class AzureOpenAI
    {
        /// <summary>Azure OpenAI API key — encrypted, T+G scope.</summary>
        public const string ApiKey = Prefix + "AzureOpenAI.ApiKey";

        /// <summary>Azure OpenAI resource endpoint — T+G scope.</summary>
        public const string Endpoint = Prefix + "AzureOpenAI.Endpoint";
    }

    /// <summary>Ollama provider settings.</summary>
    public static class Ollama
    {
        /// <summary>Ollama server endpoint URL — T+G scope (URL, not encrypted).</summary>
        public const string Endpoint = Prefix + "Ollama.Endpoint";
    }

    /// <summary>Host-only setting toggling the Host-fallback rate limit bypass.</summary>
    public const string HostFallbackBypassRateLimit = Prefix + "HostFallback.BypassRateLimit";
}
