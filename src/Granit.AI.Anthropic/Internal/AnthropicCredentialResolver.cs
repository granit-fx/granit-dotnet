using Granit.AI.Anthropic.Options;
using Granit.AI.Exceptions;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Internal;

/// <summary>
/// Resolves the credential for Anthropic workspaces.
/// </summary>
/// <remarks>
/// Anthropic has a fixed endpoint (<c>api.anthropic.com</c>), so the cascade resolves only
/// the API key:
/// <list type="number">
///   <item><see cref="AIWorkspace.ApiKey"/> &#8594; <see cref="AIProviderCredentialScope.Workspace"/></item>
///   <item>Tenant Setting <c>Granit.AI.Anthropic.ApiKey</c> &#8594; <see cref="AIProviderCredentialScope.Tenant"/></item>
///   <item>Global Setting <c>Granit.AI.Anthropic.ApiKey</c> &#8594; <see cref="AIProviderCredentialScope.Host"/></item>
///   <item><see cref="AnthropicProviderOptions.ApiKey"/> &#8594; <see cref="AIProviderCredentialScope.Host"/></item>
/// </list>
/// </remarks>
internal sealed class AnthropicCredentialResolver(
    SettingDefinitionManager definitions,
    IEnumerable<ISettingValueProvider> settingProviders,
    IOptionsMonitor<AnthropicProviderOptions> options) : IAIProviderCredentialResolver
{
    private readonly ISettingValueProvider? _tenantProvider =
        settingProviders.FirstOrDefault(p => p.Name == TenantSettingValueProvider.ProviderName);
    private readonly ISettingValueProvider? _globalProvider =
        settingProviders.FirstOrDefault(p => p.Name == GlobalSettingValueProvider.ProviderName);

    /// <inheritdoc />
    public string ProviderName => "Anthropic";

    /// <inheritdoc />
    public async ValueTask<AIProviderCredential> ResolveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        AIProviderCredential? credential = await AICredentialCascade
            .ResolveAsync(
                workspaceApiKey: workspace.ApiKey,
                workspaceEndpoint: null,                       // Anthropic has no endpoint axis.
                apiKeySettingName: AISettingNames.Anthropic.ApiKey,
                endpointSettingName: null,
                hostFallback: (options.CurrentValue.ApiKey, null),
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

        return credential;
    }
}
