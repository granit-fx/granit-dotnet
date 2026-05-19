using Granit.Settings.Definitions;

namespace Granit.AI.Tenancy;

/// <summary>
/// Declares the AI credential settings used by the cascade resolver:
/// per-provider <c>ApiKey</c> and <c>Endpoint</c> entries, encrypted at rest, never
/// exposed to clients, settable at Tenant or Global scope.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitSettingsModule</c> — no manual registration needed.
/// Writes to any setting under the <see cref="AISettingNames.PrefixValue"/> prefix
/// additionally require the <c>AI.Credentials.Manage</c> permission (enforced by
/// the Settings write guard registered by <c>AddGranitAI()</c>).
/// </remarks>
internal sealed class AISettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <inheritdoc />
    public void Define(ISettingDefinitionContext context)
    {
        // ----- Anthropic -----
        context.Add(new SettingDefinition(AISettingNames.Anthropic.ApiKey)
        {
            IsEncrypted = true,
            IsVisibleToClients = false,
            DisplayName = "Anthropic API key",
            Description = "API key used to authenticate Anthropic Claude requests. " +
                "Encrypted at rest. Settable at Tenant scope by tenant admins, or at Global " +
                "scope by host operators.",
            Providers = { "T", "G" },
        });

        // ----- OpenAI -----
        context.Add(new SettingDefinition(AISettingNames.OpenAI.ApiKey)
        {
            IsEncrypted = true,
            IsVisibleToClients = false,
            DisplayName = "OpenAI API key",
            Description = "API key used to authenticate OpenAI requests. Encrypted at rest.",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(AISettingNames.OpenAI.Endpoint)
        {
            IsEncrypted = false,
            IsVisibleToClients = false,
            DisplayName = "OpenAI endpoint override",
            Description = "Optional endpoint override for OpenAI-compatible APIs " +
                "(e.g. corporate proxies). Validated against the SSRF policy.",
            Providers = { "T", "G" },
        });

        // ----- Azure OpenAI -----
        context.Add(new SettingDefinition(AISettingNames.AzureOpenAI.ApiKey)
        {
            IsEncrypted = true,
            IsVisibleToClients = false,
            DisplayName = "Azure OpenAI API key",
            Description = "API key used to authenticate Azure OpenAI requests. " +
                "Encrypted at rest. Leave unset to fall back to Managed Identity " +
                "(requires the host to opt in via AllowManagedIdentityFallback).",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(AISettingNames.AzureOpenAI.Endpoint)
        {
            IsEncrypted = false,
            IsVisibleToClients = false,
            DisplayName = "Azure OpenAI resource endpoint",
            Description = "Resource endpoint URL (e.g. https://my-resource.openai.azure.com). " +
                "Validated against the SSRF policy.",
            Providers = { "T", "G" },
        });

        // ----- Ollama -----
        context.Add(new SettingDefinition(AISettingNames.Ollama.Endpoint)
        {
            IsEncrypted = false,
            IsVisibleToClients = false,
            DisplayName = "Ollama server endpoint",
            Description = "URL of the Ollama server. May target a tenant-managed instance. " +
                "Loopback is allowed; private-IP and link-local ranges are blocked for tenant writes.",
            Providers = { "T", "G" },
        });

        // ----- Cross-cutting -----
        context.Add(new SettingDefinition(AISettingNames.HostFallbackBypassRateLimit)
        {
            IsEncrypted = false,
            IsVisibleToClients = false,
            DisplayName = "Bypass Host-fallback rate limit",
            Description = "Set to 'true' for tenants explicitly whitelisted to bypass the " +
                "per-tenant rate limit on the Host credential fallback. Host-scope only.",
            Providers = { "G" },
            ValueKind = ValueKind.Bool,
            DefaultValue = "false",
        });
    }
}
