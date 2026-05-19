using Granit.AI.Exceptions;
using Granit.AI.OpenAI.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Microsoft.Extensions.Options;

namespace Granit.AI.OpenAI.Internal;

/// <summary>
/// Resolves the credential (ApiKey + optional Endpoint) for OpenAI workspaces.
/// </summary>
/// <remarks>
/// Cascade Workspace &#8594; Tenant Setting &#8594; Global Setting &#8594; Host Options.
/// Endpoint validation against <see cref="AIEndpointPolicy.HostedHttps"/> is performed at
/// each layer; an invalid override surfaces as an <see cref="InvalidOperationException"/>
/// rather than silently rotating to the next layer.
/// </remarks>
internal sealed class OpenAICredentialResolver(
    SettingDefinitionManager definitions,
    IEnumerable<ISettingValueProvider> settingProviders,
    IOptionsMonitor<OpenAIProviderOptions> options) : IAIProviderCredentialResolver
{
    private readonly ISettingValueProvider? _tenantProvider =
        settingProviders.FirstOrDefault(p => p.Name == TenantSettingValueProvider.ProviderName);
    private readonly ISettingValueProvider? _globalProvider =
        settingProviders.FirstOrDefault(p => p.Name == GlobalSettingValueProvider.ProviderName);

    /// <inheritdoc />
    public string ProviderName => "OpenAI";

    /// <inheritdoc />
    public async ValueTask<AIProviderCredential> ResolveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        OpenAIProviderOptions opts = options.CurrentValue;

        AIProviderCredential? credential = await AICredentialCascade
            .ResolveAsync(
                workspaceApiKey: workspace.ApiKey,
                workspaceEndpoint: workspace.Endpoint,
                apiKeySettingName: AISettingNames.OpenAI.ApiKey,
                endpointSettingName: AISettingNames.OpenAI.Endpoint,
                hostFallback: (opts.ApiKey, opts.Endpoint),
                definitions: definitions,
                tenantProvider: _tenantProvider,
                globalProvider: _globalProvider,
                workspace: workspace,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (credential is null || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            throw new AIProviderCredentialNotConfiguredException(
                ProviderName, workspace.Name, workspace.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(credential.Endpoint))
        {
            AIEndpointValidationResult result = AIEndpointValidator.Validate(
                credential.Endpoint,
                AIEndpointPolicy.HostedHttps);

            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    $"OpenAI endpoint override '{credential.Endpoint}' failed validation: {result.Error}.");
            }
        }

        return credential;
    }
}
