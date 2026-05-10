namespace Granit.Documents.Authorization;

/// <summary>
/// Snapshot of a caller's effective grantee identities — user id plus the role and group ids
/// they belong to — used to resolve <see cref="DocumentShare"/> grants against a folder or
/// document.
/// </summary>
/// <remarks>
/// <para>
/// The principal abstraction is intentionally a value object rather than tied to ASP.NET's
/// <see cref="System.Security.Claims.ClaimsPrincipal"/>: that keeps the resolver testable
/// without an HTTP context and lets non-HTTP consumers (background jobs, internal callers)
/// build a principal directly from the user record.
/// </para>
/// <para>
/// <see cref="AllGranteeIds"/> is the union (deduplicated, allocation-once) of
/// <see cref="UserId"/>, <see cref="RoleIds"/>, and <see cref="GroupIds"/> — the resolver
/// uses this set to filter <see cref="DocumentShare.GranteeId"/> in a single SQL <c>IN</c>
/// expression regardless of which kind of grantee the share targets.
/// </para>
/// </remarks>
/// <param name="UserId">Identifier of the calling user (ADR-051 <c>User.Id</c>).</param>
/// <param name="RoleIds">Role identifiers the user holds (may be empty).</param>
/// <param name="GroupIds">Group identifiers the user belongs to (may be empty).</param>
public sealed record DocumentPrincipal(
    Guid UserId,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<Guid> GroupIds)
{
    private IReadOnlyList<Guid>? _allGranteeIds;

    /// <summary>
    /// Convenience principal carrying only a user id (no roles / groups). Useful for tests
    /// and for callers that have not yet been wired to the role / group resolver.
    /// </summary>
    public static DocumentPrincipal ForUser(Guid userId) =>
        new(userId, [], []);

    /// <summary>
    /// Deduplicated union of <see cref="UserId"/>, <see cref="RoleIds"/>, and
    /// <see cref="GroupIds"/>. Computed lazily and cached so the resolver does not
    /// re-allocate on every invocation. <see cref="Guid.Empty"/> entries are filtered out.
    /// </summary>
    public IReadOnlyList<Guid> AllGranteeIds
    {
        get
        {
            if (_allGranteeIds is not null)
            {
                return _allGranteeIds;
            }

            HashSet<Guid> set = new(RoleIds.Count + GroupIds.Count + 1);
            if (UserId != Guid.Empty)
            {
                set.Add(UserId);
            }
            foreach (Guid id in RoleIds)
            {
                if (id != Guid.Empty)
                {
                    set.Add(id);
                }
            }
            foreach (Guid id in GroupIds)
            {
                if (id != Guid.Empty)
                {
                    set.Add(id);
                }
            }

            _allGranteeIds = [.. set];
            return _allGranteeIds;
        }
    }
}
