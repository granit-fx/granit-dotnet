using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Exceptions;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Resolves the credential (ApiKey + Endpoint) for Azure OpenAI workspaces.
/// </summary>
/// <remarks>
/// Cascade Workspace &#8594; Tenant Setting &#8594; Global Setting &#8594; Host Options.
/// When the cascade yields no ApiKey but <see cref="AzureOpenAIProviderOptions.AllowManagedIdentityFallback"/>
/// is <c>true</c>, the resolver emits a Managed Identity credential with
/// <see cref="AIProviderCredentialScope.ManagedIdentity"/> — observable separately in OTel.
/// Endpoint validation uses <see cref="AIEndpointPolicy.AzureOpenAI"/>.
/// </remarks>
internal sealed class AzureOpenAICredentialResolver(
    SettingDefinitionManager definitions,
    IEnumerable<ISettingValueProvider> settingProviders,
    IOptionsMonitor<AzureOpenAIProviderOptions> options) : IAIProviderCredentialResolver
{
    private readonly ISettingValueProvider? _tenantProvider =
        settingProviders.FirstOrDefault(p => p.Name == TenantSettingValueProvider.ProviderName);
    private readonly ISettingValueProvider? _globalProvider =
        settingProviders.FirstOrDefault(p => p.Name == GlobalSettingValueProvider.ProviderName);

    /// <inheritdoc />
    public string ProviderName => "AzureOpenAI";

    /// <inheritdoc />
    public async ValueTask<AIProviderCredential> ResolveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AzureOpenAIProviderOptions opts = options.CurrentValue;

        AIProviderCredential? credential = await AICredentialCascade
            .ResolveAsync(
                workspaceApiKey: workspace.ApiKey,
                workspaceEndpoint: workspace.Endpoint,
                apiKeySettingName: AISettingNames.AzureOpenAI.ApiKey,
                endpointSettingName: AISettingNames.AzureOpenAI.Endpoint,
                hostFallback: (opts.ApiKey, string.IsNullOrWhiteSpace(opts.Endpoint) ? null : opts.Endpoint),
                definitions: definitions,
                tenantProvider: _tenantProvider,
                globalProvider: _globalProvider,
                workspace: workspace,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // We need at least an endpoint to talk to Azure OpenAI.
        string? endpoint = credential?.Endpoint ?? (string.IsNullOrWhiteSpace(opts.Endpoint) ? null : opts.Endpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new AIProviderCredentialNotConfiguredException(
                ProviderName, workspace.Name, workspace.TenantId);
        }

        // SSRF validation on the resolved endpoint.
        AIEndpointValidationResult endpointValidation = AIEndpointValidator.Validate(endpoint, AIEndpointPolicy.AzureOpenAI);
        if (!endpointValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI endpoint '{endpoint}' failed validation: {endpointValidation.Error}.");
        }

        // If the cascade returned an ApiKey, use it.
        if (credential is not null && !string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            return credential with { Endpoint = endpoint };
        }

        // No ApiKey at any layer — Managed Identity is only used when the operator explicitly opts in.
        if (!opts.AllowManagedIdentityFallback)
        {
            throw new AIProviderCredentialNotConfiguredException(
                ProviderName, workspace.Name, workspace.TenantId);
        }

        return new AIProviderCredential
        {
            ApiKey = null,
            Endpoint = endpoint,
            Scope = AIProviderCredentialScope.ManagedIdentity,
            BilledToTenantId = workspace.TenantId,
        };
    }
}
