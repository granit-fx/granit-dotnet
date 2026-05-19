using Granit.AI.Workspaces;

namespace Granit.AI.Tenancy;

/// <summary>
/// Resolves the credential to use for a given workspace, following the cascade
/// Workspace &#8594; Tenant Setting &#8594; Global Setting &#8594; Host Options.
/// </summary>
/// <remarks>
/// <para>
/// Each provider package (Granit.AI.Anthropic, Granit.AI.OpenAI, ...) ships a concrete
/// implementation. Hosts wanting Vault-backed resolution can replace a specific resolver
/// via <c>services.Replace(...)</c>.
/// </para>
/// <para>
/// Implementations must be registered as Scoped — they depend on Scoped services
/// (<c>ISettingProvider</c>, <c>ICurrentTenant</c>).
/// </para>
/// </remarks>
public interface IAIProviderCredentialResolver
{
    /// <summary>
    /// Provider identifier this resolver serves (e.g. <c>Anthropic</c>, <c>OpenAI</c>).
    /// Matches <c>IAIProviderFactory.ProviderName</c>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Resolves a credential for the given workspace.
    /// </summary>
    /// <param name="workspace">The workspace whose call is being prepared.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The credential to use, with attribution metadata.</returns>
    /// <exception cref="AIProviderCredentialNotConfiguredException">
    /// Thrown when no layer of the cascade provides a usable credential.
    /// </exception>
    ValueTask<AIProviderCredential> ResolveAsync(
        AIWorkspace workspace,
        CancellationToken cancellationToken = default);
}
