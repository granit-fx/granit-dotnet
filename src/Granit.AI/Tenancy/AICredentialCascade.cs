using Granit.AI.Workspaces;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Values;

namespace Granit.AI.Tenancy;

/// <summary>
/// Shared resolution helper invoked by each provider's <see cref="IAIProviderCredentialResolver"/>.
/// Implements the four-layer cascade Workspace &#8594; Tenant &#8594; Global &#8594; Host Options,
/// collapsing Tenant/Global writes from <see cref="ISettingValueProvider"/> into the typed
/// <see cref="AIProviderCredential"/> result.
/// </summary>
internal static class AICredentialCascade
{
    /// <summary>
    /// Resolves the credential for a workspace using a per-provider lookup function.
    /// The <paramref name="hostFallback"/> is the provider options snapshot (ApiKey, Endpoint).
    /// When the cascade exhausts all layers without finding a usable credential, this method
    /// returns <c>null</c> and lets the caller throw or supply a provider-specific default
    /// (e.g. Managed Identity).
    /// </summary>
    /// <param name="workspaceApiKey">Workspace-scoped ApiKey override.</param>
    /// <param name="workspaceEndpoint">Workspace-scoped Endpoint override.</param>
    /// <param name="apiKeySettingName">Setting name used to query Tenant + Global ApiKey.</param>
    /// <param name="endpointSettingName">Setting name used to query Tenant + Global Endpoint. <c>null</c> when the provider has no endpoint axis.</param>
    /// <param name="hostFallback">Host-Options snapshot.</param>
    /// <param name="definitions">Setting definition manager.</param>
    /// <param name="tenantProvider">"T" ISettingValueProvider (or <c>null</c> when Settings is not configured).</param>
    /// <param name="globalProvider">"G" ISettingValueProvider.</param>
    /// <param name="workspace">Workspace being resolved (for BilledToTenantId).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public static async ValueTask<AIProviderCredential?> ResolveAsync(
        string? workspaceApiKey,
        string? workspaceEndpoint,
        string? apiKeySettingName,
        string? endpointSettingName,
        (string? ApiKey, string? Endpoint) hostFallback,
        SettingDefinitionManager definitions,
        ISettingValueProvider? tenantProvider,
        ISettingValueProvider? globalProvider,
        AIWorkspace workspace,
        CancellationToken cancellationToken)
    {
        // Layer 1: Workspace — applies if EITHER ApiKey or Endpoint is set.
        if (!string.IsNullOrWhiteSpace(workspaceApiKey) || !string.IsNullOrWhiteSpace(workspaceEndpoint))
        {
            return new AIProviderCredential
            {
                ApiKey = workspaceApiKey,
                Endpoint = workspaceEndpoint,
                Scope = AIProviderCredentialScope.Workspace,
                BilledToTenantId = workspace.TenantId,
            };
        }

        // Layer 2: Tenant Setting.
        SettingDefinition? apiKeyDefinition =
            apiKeySettingName is null ? null : definitions.Get(apiKeySettingName);
        SettingDefinition? endpointDefinition =
            endpointSettingName is null ? null : definitions.Get(endpointSettingName);

        (string? tenantApiKey, string? tenantEndpoint) =
            await ReadLayerAsync(tenantProvider, apiKeyDefinition, endpointDefinition, cancellationToken)
                .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(tenantApiKey) || !string.IsNullOrWhiteSpace(tenantEndpoint))
        {
            return new AIProviderCredential
            {
                ApiKey = tenantApiKey,
                Endpoint = tenantEndpoint,
                Scope = AIProviderCredentialScope.Tenant,
                BilledToTenantId = workspace.TenantId,
            };
        }

        // Layer 3: Global Setting.
        (string? globalApiKey, string? globalEndpoint) =
            await ReadLayerAsync(globalProvider, apiKeyDefinition, endpointDefinition, cancellationToken)
                .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(globalApiKey) || !string.IsNullOrWhiteSpace(globalEndpoint))
        {
            return new AIProviderCredential
            {
                ApiKey = globalApiKey,
                Endpoint = globalEndpoint,
                Scope = AIProviderCredentialScope.Host,
                BilledToTenantId = workspace.TenantId,
            };
        }

        // Layer 4: Host Options.
        if (!string.IsNullOrWhiteSpace(hostFallback.ApiKey) || !string.IsNullOrWhiteSpace(hostFallback.Endpoint))
        {
            return new AIProviderCredential
            {
                ApiKey = hostFallback.ApiKey,
                Endpoint = hostFallback.Endpoint,
                Scope = AIProviderCredentialScope.Host,
                BilledToTenantId = workspace.TenantId,
            };
        }

        return null;
    }

    private static async ValueTask<(string? ApiKey, string? Endpoint)> ReadLayerAsync(
        ISettingValueProvider? provider,
        SettingDefinition? apiKeyDefinition,
        SettingDefinition? endpointDefinition,
        CancellationToken cancellationToken)
    {
        if (provider is null)
        {
            return (null, null);
        }

        SettingValue? apiKeyValue = apiKeyDefinition is null
            ? null
            : await provider.GetOrNullAsync(apiKeyDefinition, cancellationToken).ConfigureAwait(false);

        SettingValue? endpointValue = endpointDefinition is null
            ? null
            : await provider.GetOrNullAsync(endpointDefinition, cancellationToken).ConfigureAwait(false);

        return (apiKeyValue?.Value, endpointValue?.Value);
    }
}
