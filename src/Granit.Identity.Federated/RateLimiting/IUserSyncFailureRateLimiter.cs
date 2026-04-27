namespace Granit.Identity.Federated.RateLimiting;

/// <summary>
/// Throttles <c>IdentityUserSyncFailedEto</c> emissions per
/// (<c>UserId</c>, <c>ProviderName</c>) so a single mis-provisioned user
/// cannot flood the notification channel during a sync-loop incident.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="InMemoryUserSyncFailureRateLimiter"/> is process-local
/// — adequate for single-pod deployments and keeps the rollout dependency-free.
/// Multi-pod hosts should replace the registration with a
/// <c>Granit.RateLimiting</c>-backed implementation so the cool-off window is
/// enforced cluster-wide.
/// </para>
/// </remarks>
public interface IUserSyncFailureRateLimiter
{
    /// <summary>
    /// Reserves the next emission slot for (<paramref name="userId"/>,
    /// <paramref name="providerName"/>). Returns <c>true</c> when the caller
    /// should emit the Eto (and consumes the slot for the configured cool-off
    /// window); returns <c>false</c> when an emission for the same key is
    /// already in-flight for this window.
    /// </summary>
    /// <param name="userId">External user identifier in the identity provider.</param>
    /// <param name="providerName">Logical provider name (e.g. <c>"Keycloak"</c>).</param>
    /// <returns><c>true</c> if the emission is allowed; <c>false</c> if it should be suppressed.</returns>
    bool TryAcquire(string userId, string providerName);
}
