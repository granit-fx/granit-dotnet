using Granit.AI.Exceptions;
using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Microsoft.Extensions.Options;

namespace Granit.AI.Ollama.Internal;

/// <summary>
/// Resolves the credential (Endpoint only) for Ollama workspaces.
/// </summary>
/// <remarks>
/// Ollama has no API key — the Endpoint URL is the credential (a tenant may have its own
/// Ollama deployment). Cascade: Workspace.Endpoint &#8594; Tenant Setting Endpoint &#8594;
/// Global Setting Endpoint &#8594; Host Options Endpoint. Endpoint validation against
/// <see cref="AIEndpointPolicy.OllamaTenant"/> for Workspace/Tenant/Global layers; the Host
/// Options layer uses the permissive policy (operator-trusted).
/// </remarks>
internal sealed class OllamaCredentialResolver(
    SettingDefinitionManager definitions,
    IEnumerable<ISettingValueProvider> settingProviders,
    IOptionsMonitor<OllamaProviderOptions> options) : IAIProviderCredentialResolver
{
    private readonly ISettingValueProvider? _tenantProvider =
        settingProviders.FirstOrDefault(p => p.Name == TenantSettingValueProvider.ProviderName);
    private readonly ISettingValueProvider? _globalProvider =
        settingProviders.FirstOrDefault(p => p.Name == GlobalSettingValueProvider.ProviderName);

    /// <inheritdoc />
    public string ProviderName => "Ollama";

    /// <inheritdoc />
    public async ValueTask<AIProviderCredential> ResolveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OllamaProviderOptions opts = options.CurrentValue;

        // Drive the cascade on the Endpoint axis (Ollama has no ApiKey).
        AIProviderCredential? credential = await AICredentialCascade
            .ResolveAsync(
                workspaceApiKey: null,
                workspaceEndpoint: workspace.Endpoint,
                apiKeySettingName: null,                            // Ollama has no ApiKey axis.
                endpointSettingName: AISettingNames.Ollama.Endpoint,
                hostFallback: (null, string.IsNullOrWhiteSpace(opts.Endpoint) ? null : opts.Endpoint),
                definitions: definitions,
                tenantProvider: _tenantProvider,
                globalProvider: _globalProvider,
                workspace: workspace,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Cascade returns null if no layer supplies an Endpoint — fall back to options Endpoint
        // (the cascade already covers that via hostFallback, so null here means truly nothing).
        if (credential is null || string.IsNullOrWhiteSpace(credential.Endpoint))
        {
            throw new AIProviderCredentialNotConfiguredException(
                ProviderName, workspace.Name, workspace.TenantId);
        }

        AIEndpointPolicy policy = credential.Scope == AIProviderCredentialScope.Host
            ? AIEndpointPolicy.HostPermissive
            : AIEndpointPolicy.OllamaTenant;

        AIEndpointValidationResult endpointValidation = AIEndpointValidator.Validate(credential.Endpoint, policy);
        if (!endpointValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Ollama endpoint '{credential.Endpoint}' failed validation: {endpointValidation.Error}.");
        }

        return credential;
    }
}
