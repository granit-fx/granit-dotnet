using Granit.Wolverine.Internal;
using Granit.Wolverine.Middleware;
using Wolverine;

namespace Granit.Wolverine.Behaviors;

/// <summary>
/// Wolverine middleware that restores the current user context from incoming
/// message envelope headers in background handler threads.
/// </summary>
/// <remarks>
/// <para>
/// Reads the <c>X-User-Id</c> header set by
/// <see cref="OutgoingContextMiddleware"/> on the publisher side and activates
/// the corresponding user ID for the duration of the handler invocation via
/// <see cref="IWolverineUserContextSetter.Change"/>.
/// </para>
/// <para>
/// Without this behavior, <c>ICurrentUserService.UserId</c> returns null in background
/// handlers (no <c>HttpContext</c>), causing the EF Core audit interceptor to record
/// <c>ModifiedBy = null</c> in the ISO 27001 audit trail.
/// </para>
/// <para>
/// If the header is absent, no override is applied and the handler executes with
/// the default <c>ICurrentUserService</c> (returns null for background threads).
/// </para>
/// </remarks>
public sealed class UserContextBehavior(IWolverineUserContextSetter setter)
{
    private IDisposable? _scope;

    /// <summary>
    /// Activates the user from the <c>X-User-Id</c> header before the handler runs.
    /// Also restores <c>X-Actor-Kind</c> and <c>X-Api-Key-Id</c> if present.
    /// </summary>
    /// <param name="envelope">The incoming Wolverine envelope.</param>
    public void Before(Envelope envelope)
    {
        if (envelope.Headers.TryGetValue(OutgoingContextMiddleware.UserIdHeader, out string? userId)
            && !string.IsNullOrEmpty(userId))
        {
            envelope.Headers.TryGetValue(OutgoingContextMiddleware.UserFirstNameHeader, out string? firstName);
            envelope.Headers.TryGetValue(OutgoingContextMiddleware.UserLastNameHeader, out string? lastName);

            Users.ActorKind actorKind = envelope.Headers.TryGetValue(OutgoingContextMiddleware.ActorKindHeader, out string? ak)
                && Enum.TryParse<Users.ActorKind>(ak, out Users.ActorKind parsed)
                    ? parsed
                    : Users.ActorKind.User;

            Guid? apiKeyId = envelope.Headers.TryGetValue(OutgoingContextMiddleware.ApiKeyIdHeader, out string? akId)
                && Guid.TryParse(akId, out Guid parsedId)
                    ? parsedId
                    : null;

            _scope = setter.Change(userId, firstName, lastName, actorKind, apiKeyId);
        }
    }

    /// <summary>Restores the previous user context after the handler completes.</summary>
    public void After() => _scope?.Dispose();
}
