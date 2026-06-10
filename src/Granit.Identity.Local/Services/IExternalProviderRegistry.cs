namespace Granit.Identity.Local.Services;

/// <summary>
/// Registry for configured external authentication providers (Google, Microsoft, GitHub, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Implementations read the providers configured under <c>Authentication:External:Providers</c>
/// (owned by <c>Granit.Authentication.External</c>); the registry surfaces which of them are
/// usable for an OAuth challenge.
/// </para>
/// <para>
/// Used by account self-service endpoints to validate that a requested external
/// provider is actually configured before initiating an OAuth challenge.
/// </para>
/// </remarks>
public interface IExternalProviderRegistry
{
    /// <summary>
    /// Returns the names of all configured external providers.
    /// </summary>
    IReadOnlyList<string> GetConfiguredProviderNames();

    /// <summary>
    /// Returns <see langword="true"/> if the given provider name is configured.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "Google", "Microsoft").</param>
    bool IsProviderConfigured(string providerName);

    /// <summary>
    /// Returns <see langword="true"/> only if the provider is both configured
    /// <em>and</em> backed by a registered authentication handler — i.e. an OAuth challenge
    /// for it would actually succeed.
    /// </summary>
    /// <remarks>
    /// A provider can be listed in configuration without the host having wired its
    /// authentication scheme (<c>AddGoogle()</c>, <c>AddMicrosoftAccount()</c>, …). In that
    /// case <see cref="IsProviderConfigured"/> returns <see langword="true"/> but the
    /// subsequent challenge dead-ends at the redirect. This method closes that gap.
    /// </remarks>
    /// <param name="providerName">The provider name (e.g., "Google", "Microsoft").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> IsProviderAvailableAsync(string providerName, CancellationToken cancellationToken = default);
}
