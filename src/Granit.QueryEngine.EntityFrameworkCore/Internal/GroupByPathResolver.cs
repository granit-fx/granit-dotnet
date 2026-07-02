using System.Linq.Expressions;
using System.Reflection;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Builds the group-by key-selector body for a (possibly dotted) member path such as
/// <c>"Kind"</c> or <c>"Value.Country"</c>. A dotted path walks EF Core complex-type hops with
/// chained <see cref="Expression.Property(Expression, PropertyInfo)"/>; the leaf still goes through
/// <see cref="ValueObjectMemberResolver"/> so a <c>[QueryableValueObject]</c> column groups by its
/// underlying <c>.Value</c> scalar (ADR-070). Shared by <see cref="QueryableGroupByExtensions"/>
/// (SQL key selector) and the in-memory item bucketing so both resolve to the same key.
/// </summary>
internal static class GroupByPathResolver
{
    /// <summary>
    /// Resolves the key-access expression for <paramref name="path"/> rooted at
    /// <paramref name="instance"/>. Returns <see langword="null"/> when any segment cannot be
    /// resolved (unknown field), matching the legacy "empty grouped result" behavior.
    /// </summary>
    public static Expression? BuildKeyAccess(Expression instance, string path)
    {
        string[] segments = path.Split('.');
        Expression parent = instance;

        for (int i = 0; i < segments.Length; i++)
        {
            PropertyInfo? property = parent.Type.GetProperty(
                segments[i],
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                return null;
            }

            if (i == segments.Length - 1)
            {
                return ValueObjectMemberResolver.Resolve(parent, property);
            }

            parent = Expression.Property(parent, property);
        }

        return null;
    }
}
