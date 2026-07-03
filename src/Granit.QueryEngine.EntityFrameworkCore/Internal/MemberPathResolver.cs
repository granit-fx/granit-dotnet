using System.Linq.Expressions;
using System.Reflection;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Walks a (possibly dotted) member path such as <c>"Kind"</c> or <c>"Value.Street1"</c> rooted at an
/// arbitrary expression, chaining <see cref="Expression.Property(Expression, PropertyInfo)"/> for each
/// EF Core complex-type hop. The leaf goes through <see cref="ValueObjectMemberResolver"/> so a
/// <c>[QueryableValueObject]</c> leaf resolves to its underlying <c>.Value</c> scalar (ADR-070).
/// Shared by filtering, sorting, and group-by so a nested column resolves identically everywhere.
/// </summary>
internal static class MemberPathResolver
{
    /// <summary>
    /// Resolves the member access for <paramref name="path"/> rooted at <paramref name="instance"/>,
    /// returning both the (VO-drilled) leaf access expression and the leaf <see cref="PropertyInfo"/>.
    /// Returns <c>(null, null)</c> when any segment cannot be resolved (unknown field), matching the
    /// existing "unresolved field is dropped / empty result" behavior.
    /// </summary>
    public static (Expression? Member, PropertyInfo? Leaf) Resolve(Expression instance, string path)
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
                return (null, null);
            }

            if (i == segments.Length - 1)
            {
                return (ValueObjectMemberResolver.Resolve(parent, property), property);
            }

            parent = Expression.Property(parent, property);
        }

        return (null, null);
    }
}
