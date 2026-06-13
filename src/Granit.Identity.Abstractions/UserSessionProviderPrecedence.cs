namespace Granit.Identity;

/// <summary>
/// Explicit precedence for backend <see cref="IUserSessionProvider"/> / <see cref="IUserDeviceProvider"/>
/// registrations. When several backends are present on one host (e.g. OpenIddict + BFF), the highest
/// precedence wins each facet — <strong>deterministically, independent of registration order</strong>
/// (module topological order or the order of imperative <c>AddGranit*</c> calls).
/// </summary>
/// <remarks>
/// Backends register through <c>SetUserSessionProvider</c> rather than calling
/// <c>IServiceCollection.Replace</c> directly, so the winner is a single explicit value here instead of an
/// accident of ordering. To change the winner, change the precedence a backend registers with.
/// </remarks>
public enum UserSessionProviderPrecedence
{
    /// <summary>The no-op defaults. Replaced by any real backend.</summary>
    None = 0,

    /// <summary>
    /// Federated identity providers (Entra ID, Cognito, Keycloak): sessions/devices read from the
    /// external IdP.
    /// </summary>
    Federated = 10,

    /// <summary>OpenIddict authority: one session per live refresh-token grant.</summary>
    OpenIddict = 20,

    /// <summary>
    /// BFF gateway: the user-facing browser↔BFF session — the one the user revokes from the canonical
    /// API. Wins the session facet by default when it coexists with another backend.
    /// </summary>
    Bff = 30,
}
