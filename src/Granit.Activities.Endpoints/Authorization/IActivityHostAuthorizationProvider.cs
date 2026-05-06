using System.Security.Claims;

namespace Granit.Activities.Endpoints.Authorization;

/// <summary>
/// Per-host authorization gate for the polymorphic <c>(EntityType, EntityId)</c>
/// pinning of an <see cref="Granit.Activities.Domain.Activity"/>. Closes
/// VULN-102 / VULN-202 — without this gate, any caller with
/// <c>Activities.Activities.Read</c> could fingerprint workflows pinned to
/// host entities they cannot otherwise read (e.g. activities pinned to
/// <c>Granit.Identity.User</c> without holding <c>Identity.Users.Read</c>).
/// </summary>
/// <remarks>
/// Each module that contributes activity-bearing entities registers an
/// implementation via <c>services.AddSingleton&lt;IActivityHostAuthorizationProvider, …&gt;()</c>.
/// The endpoint layer composes all registered providers and returns
/// <see langword="true"/> only if every applicable provider authorizes
/// the host. The default implementation
/// (<see cref="AllowAllActivityHostAuthorizationProvider"/>) returns
/// <see langword="true"/> for every host so single-tenant or low-sensitivity
/// hosts do not require explicit registration.
/// </remarks>
public interface IActivityHostAuthorizationProvider
{
    /// <summary>
    /// Wire identifier of the host entity this provider gates. Use
    /// <c>"*"</c> to apply to every host (catch-all default).
    /// </summary>
    string EntityType { get; }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="user"/> is allowed
    /// to see activities pinned to <paramref name="entityId"/>.
    /// Implementations should fail closed (return <see langword="false"/>)
    /// on any error.
    /// </summary>
    ValueTask<bool> CanReadHostAsync(Guid entityId, ClaimsPrincipal user, CancellationToken cancellationToken);
}

/// <summary>
/// Catch-all default that authorizes every host. Registered automatically by
/// <c>AddGranitActivitiesEndpoints()</c> so hosts that do not opt-in to per-host
/// authorization keep the previous (less strict) behavior. Hosts that contribute
/// sensitive entities replace this by registering a stricter provider for the
/// matching <see cref="EntityType"/>.
/// </summary>
public sealed class AllowAllActivityHostAuthorizationProvider : IActivityHostAuthorizationProvider
{
    /// <inheritdoc/>
    public string EntityType => "*";

    /// <inheritdoc/>
    public ValueTask<bool> CanReadHostAsync(Guid entityId, ClaimsPrincipal user, CancellationToken cancellationToken)
        => ValueTask.FromResult(true);
}
