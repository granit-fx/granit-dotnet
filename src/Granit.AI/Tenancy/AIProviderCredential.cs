namespace Granit.AI.Tenancy;

/// <summary>
/// Outcome of <see cref="IAIProviderCredentialResolver.ResolveAsync"/>.
/// Carries the secret material plus the attribution metadata needed for billing and audit.
/// </summary>
/// <remarks>
/// The record is short-lived: consumed by the provider factory to build (or look up cached) an SDK
/// client. The factory MUST NOT store the <see cref="ApiKey"/> in long-lived state. The decorator
/// MUST NOT emit <see cref="ApiKey"/> as an OTel tag; only the <see cref="Scope"/> and
/// <see cref="BilledToTenantId"/> are safe to record.
/// </remarks>
public sealed record AIProviderCredential
{
    /// <summary>
    /// API key for the provider, or <c>null</c> when the provider does not use a key
    /// (Ollama, or AzureOpenAI under Managed Identity).
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Endpoint override for providers that support it (Ollama, AzureOpenAI, OpenAI-compatible proxies),
    /// or <c>null</c> when the provider's default endpoint should be used.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>Where this credential came from in the cascade.</summary>
    public required AIProviderCredentialScope Scope { get; init; }

    /// <summary>
    /// Tenant ID to attribute usage to for billing/audit. Equal to the workspace's
    /// <c>TenantId</c> when present; <c>null</c> for system-scope calls.
    /// </summary>
    /// <remarks>
    /// This stays populated even when <see cref="Scope"/> is <see cref="AIProviderCredentialScope.Host"/>:
    /// the host's credential carried the call, but the bill is attributed to the consuming tenant.
    /// </remarks>
    public Guid? BilledToTenantId { get; init; }
}
