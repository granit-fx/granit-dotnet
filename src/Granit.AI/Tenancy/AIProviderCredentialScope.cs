namespace Granit.AI.Tenancy;

/// <summary>
/// Source layer that produced an <see cref="AIProviderCredential"/>.
/// </summary>
/// <remarks>
/// Recorded on every chat/embedding call (via OTel span tags) so audit and billing can attribute
/// usage. Cascading order, highest priority first: <see cref="Workspace"/>, <see cref="Tenant"/>,
/// <see cref="Host"/>. <see cref="ManagedIdentity"/> is only produced by Azure OpenAI when its
/// explicit <c>AllowManagedIdentityFallback</c> opt-in is enabled.
/// </remarks>
public enum AIProviderCredentialScope
{
    /// <summary>Credential carried by the <see cref="Workspaces.AIWorkspace"/> itself.</summary>
    Workspace,

    /// <summary>Credential resolved from a tenant-scoped <c>Granit.Settings</c> entry.</summary>
    Tenant,

    /// <summary>
    /// Credential resolved from a host-scope source: a Global <c>Granit.Settings</c> entry, or
    /// the legacy provider options (<c>*ProviderOptions.ApiKey</c>/<c>Endpoint</c>).
    /// </summary>
    /// <remarks>
    /// When a tenant has no per-tenant credential, the call still goes through the host account.
    /// <see cref="AIProviderCredential.BilledToTenantId"/> stays populated so usage can be
    /// attributed to the tenant that consumed it.
    /// </remarks>
    Host,

    /// <summary>
    /// Azure-only: credential resolution fell through to <c>DefaultAzureCredential</c>
    /// (Managed Identity). Requires the host operator to have set
    /// <c>AzureOpenAIProviderOptions.AllowManagedIdentityFallback = true</c>.
    /// </summary>
    ManagedIdentity,
}
