namespace Granit.Users;

/// <summary>
/// Service for accessing information about the current actor (user or machine).
/// </summary>
/// <remarks>
/// <para>
/// Default interface methods (<see cref="ActorKind"/>, <see cref="IsMachine"/>,
/// <see cref="ApiKeyId"/>) were added to support machine-to-machine authentication
/// (API keys) without breaking existing implementations. Implementations that do not
/// override these members get the defaults for a human user context.
/// </para>
/// </remarks>
public interface ICurrentUserService
{
    /// <summary>Unique identifier of the user (claim "sub").</summary>
    string? UserId { get; }

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
