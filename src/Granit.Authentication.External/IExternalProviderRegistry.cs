namespace Granit.Authentication.External;

/// <summary>
/// Registry for external authentication providers (Google, Microsoft, Apple, GitHub, Facebook,
/// generic OIDC). Surfaces which configured providers are actually usable for an OAuth challenge.
/// </summary>
/// <remarks>
/// Reads the providers configured under <c>Authentication:External:Providers</c> and cross-checks
/// them against the registered authentication schemes. Consumed by account self-service endpoints
/// (to validate a challenge target) and the anonymous login-screen config.
/// </remarks>
public interface IExternalProviderRegistry
{
    /// <summary>
    /// Returns the scheme names of all configured external providers.
    /// </summary>
    IReadOnlyList<string> GetConfiguredProviderNames();

    /// <summary>
    /// Returns <see langword="true"/> if the given provider name is configured.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "Google", "Microsoft").</param>
    bool IsProviderConfigured(string providerName);

    /// <summary>
    /// Resolves the canonical authentication scheme name for a configured provider, matching the
    /// provider name case-insensitively. Returns <see langword="null"/> if not configured. Use this
    /// to issue an OAuth challenge against the exact scheme the host registered, avoiding a casing
    /// mismatch between the request and <c>AddGoogle()</c>/<c>AddMicrosoftAccount()</c>.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "google", "Microsoft").</param>
    string? GetSchemeName(string providerName);

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

    /// <summary>
    /// Returns metadata for every provider that is configured AND backed by a registered
    /// authentication handler (i.e. usable for an OAuth challenge). Intended for the anonymous
    /// login screen to render its "Continue with …" buttons without proposing a provider whose
    /// challenge would dead-end.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ExternalProviderInfo>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for an available external authentication provider.
/// </summary>
/// <param name="Name">The authentication scheme name — the identifier passed to <c>challenge/{provider}</c>.</param>
/// <param name="Type">The provider kind (<c>Google</c>, <c>Microsoft</c>, <c>Apple</c>, <c>GitHub</c>, <c>Facebook</c>, <c>Oidc</c>).</param>
/// <param name="DisplayName">A human-friendly label for the provider.</param>
public sealed record ExternalProviderInfo(string Name, string Type, string DisplayName);
