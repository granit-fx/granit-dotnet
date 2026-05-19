using Granit.AI.Tenancy;

namespace Granit.AI.Exceptions;

/// <summary>
/// Raised when <see cref="IAIProviderCredentialResolver.ResolveAsync"/> cannot find a credential
/// at any layer of the cascade (Workspace, Tenant Setting, Global Setting, Host Options).
/// </summary>
public sealed class AIProviderCredentialNotConfiguredException : InvalidOperationException
{
    /// <summary>Provider identifier (e.g. <c>Anthropic</c>).</summary>
    public string ProviderName { get; }

    /// <summary>Workspace name being resolved.</summary>
    public string WorkspaceName { get; }

    /// <summary>Tenant ID owning the workspace, or <c>null</c> for system-scope.</summary>
    public Guid? TenantId { get; }

    /// <summary>Initializes the exception with attribution context.</summary>
    public AIProviderCredentialNotConfiguredException(
        string providerName,
        string workspaceName,
        Guid? tenantId)
        : base(BuildMessage(providerName, workspaceName, tenantId))
    {
        ProviderName = providerName;
        WorkspaceName = workspaceName;
        TenantId = tenantId;
    }

    private static string BuildMessage(string providerName, string workspaceName, Guid? tenantId) =>
        tenantId is null
            ? $"AI provider '{providerName}' has no credential configured for system workspace '{workspaceName}'. " +
              $"Set the host options or a Global setting (Granit.AI.{providerName}.ApiKey)."
            : $"AI provider '{providerName}' has no credential configured for workspace '{workspaceName}' " +
              $"(tenant {tenantId}). Configure it on the workspace, via the tenant setting " +
              $"'Granit.AI.{providerName}.ApiKey', or fall back to the host configuration.";
}
