namespace Granit.Users;

/// <summary>
/// Service for accessing information about the current actor (user or machine).
/// </summary>
/// <remarks>
/// <para>
/// Default interface methods (<see cref="ActorKind"/>, <see cref="IsMachine"/>,
/// <see cref="ApiKeyId"/>, <see cref="UserGuid"/>) were added to support
/// machine-to-machine authentication (API keys) and typed-identity propagation
/// without breaking existing implementations. Implementations that do not
/// override these members get the defaults for a human user context.
/// </para>
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>Unique identifier of the user (claim "sub").</summary>
    string? UserId { get; }

    /// <summary>
    /// The current actor's local aggregate id, when the IDP <c>sub</c> claim is
    /// parseable as a <see cref="Guid"/>. Returns <c>null</c> for non-Guid subs
    /// (e.g. Cognito federated identities prefixed with the pool region, Google's
    /// numeric sub) or unauthenticated requests.
    /// </summary>
    /// <remarks>
    /// Best-effort default impl: callers requiring an owner identity MUST handle the
    /// <c>null</c> case explicitly — typically by throwing at the command-handler
    /// or aggregate-factory boundary, so the failure surfaces before persistence
    /// rather than as a silent <see cref="Guid.Empty"/> downstream.
    /// </remarks>
    Guid? UserGuid => Guid.TryParse(UserId, out Guid id) ? id : null;

    /// <summary>Username (according to the <c>NameClaimType</c> configured in JWT Bearer).</summary>
    string? UserName { get; }

    /// <summary>Email address of the user.</summary>
    string? Email { get; }

    /// <summary>First name of the user (claim "given_name").</summary>
    string? FirstName { get; }

    /// <summary>Last name of the user (claim "family_name").</summary>
    string? LastName { get; }

    /// <summary>Indicates whether the user is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Roles assigned to the user.</summary>
    IReadOnlyList<string> GetRoles();

    /// <summary>Checks whether the user has a given role.</summary>
    bool IsInRole(string role);

    /// <summary>
    /// The kind of actor performing the current operation.
    /// Defaults to <see cref="Users.ActorKind.User"/> for backward compatibility.
    /// </summary>
    ActorKind ActorKind => ActorKind.User;

    /// <summary>
    /// Whether the current actor is a machine (external system or internal process)
    /// rather than a human user.
    /// </summary>
    bool IsMachine => ActorKind is not ActorKind.User;

    /// <summary>
    /// API key identifier when authenticated via API key, <c>null</c> otherwise.
    /// </summary>
    Guid? ApiKeyId => null;

    /// <summary>
    /// OIDC <c>client_id</c> of the calling application, <c>null</c> when unavailable
    /// (e.g. local dev hosts without an IDP, or tests).
    /// </summary>
    /// <remarks>
    /// Used by <c>ClientPermissionGrantProvider</c> (Granit.Authorization) to resolve
    /// client-level permission grants. Default implementation returns <c>null</c> so
    /// existing <see cref="ICurrentUserService"/> impls remain source-compatible.
    /// </remarks>
    string? ClientId => null;
}
