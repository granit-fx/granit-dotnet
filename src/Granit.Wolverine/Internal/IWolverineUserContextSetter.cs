using Granit.Users;

namespace Granit.Wolverine.Internal;

/// <summary>
/// Internal contract for setting the current user context in a Wolverine handler scope.
/// Implemented by <see cref="WolverineCurrentUserService"/>.
/// </summary>
public interface IWolverineUserContextSetter
{
    /// <summary>
    /// Temporarily overrides the user context for the current async flow.
    /// Dispose the returned scope to restore the previous values.
    /// </summary>
    /// <param name="userId">The user ID to set.</param>
    /// <param name="actorKind">The actor kind (optional, defaults to <see cref="ActorKind.User"/>).</param>
    /// <param name="apiKeyId">The API key ID when <paramref name="actorKind"/> is <see cref="ActorKind.ExternalSystem"/> (optional).</param>
    /// <remarks>
    /// First name and last name are intentionally not propagated through the message bus
    /// to comply with GDPR Art. 5(1)(c) — data minimization. Background handlers that need
    /// display names should resolve them on demand from the identity store.
    /// </remarks>
    IDisposable Change(
        string? userId,
        ActorKind actorKind = ActorKind.User,
        Guid? apiKeyId = null);
}
