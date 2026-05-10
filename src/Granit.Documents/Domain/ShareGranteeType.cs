namespace Granit.Documents.Domain;

/// <summary>
/// The kind of principal a <see cref="DocumentShare"/> is granted to.
/// </summary>
/// <remarks>
/// The <see cref="DocumentShare.GranteeId"/> is a plain <see cref="Guid"/> regardless of type;
/// resolution against the user's effective principal set (user id ∪ role ids ∪ group ids) is
/// performed by the F6.2 effective-permission resolver.
/// </remarks>
public enum ShareGranteeType
{
    /// <summary>Grant targets a single user (matches <c>User.Id</c>).</summary>
    User,

    /// <summary>Grant targets a role (matches any of the principal's role ids).</summary>
    Role,

    /// <summary>Grant targets a group (matches any of the principal's group ids).</summary>
    Group,
}
