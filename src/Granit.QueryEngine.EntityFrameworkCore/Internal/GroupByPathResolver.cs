using System.Linq.Expressions;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Builds the group-by key-selector body for a (possibly dotted) member path such as
/// <c>"Kind"</c> or <c>"Value.Country"</c>, delegating the walk to <see cref="MemberPathResolver"/>.
/// Shared by <see cref="QueryableGroupByExtensions"/> (SQL key selector) and the in-memory item
/// bucketing so both resolve to the same key.
/// </summary>
internal static class GroupByPathResolver
{
    /// <summary>
    /// Resolves the key-access expression for <paramref name="path"/> rooted at
    /// <paramref name="instance"/>. Returns <see langword="null"/> when any segment cannot be
    /// resolved (unknown field), matching the legacy "empty grouped result" behavior.
    /// </summary>
    public static Expression? BuildKeyAccess(Expression instance, string path) =>
        MemberPathResolver.Resolve(instance, path).Member;
}
